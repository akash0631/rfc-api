FUNCTION zsdc_store_pickbin_save .
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_COMPLETE_FLAG) TYPE  CHAR1
*"     VALUE(IM_PICNR) TYPE  ZPICNR
*"     VALUE(IM_BIN) TYPE  LGPLA
*"     VALUE(IM_SITE) TYPE  WERKS_D
*"  EXPORTING
*"     VALUE(EX_SAVE) TYPE  CHAR1
*"     VALUE(EX_MAT_NO) TYPE  BAPI2017_GM_HEAD_RET-MAT_DOC
*"     VALUE(EX_YEAR) TYPE  BAPI2017_GM_HEAD_RET-DOC_YEAR
*"     VALUE(EX_TANUM) TYPE  TANUM
*"     VALUE(EX_TO_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_MESSAGE) TYPE  CHAR120
*"     VALUE(EX_TANUM_BIN) TYPE  TANUM
*"  TABLES
*"      RETURN STRUCTURE  BAPIRET2
*"      IT_HUSAVE STRUCTURE  ZWM_ST_HU_MVT311
*"      IMT_HU_STATUS STRUCTURE  ZST_HU_PUTWAY
*"      IMT_LQUA STRUCTURE  ZST_LQUA
*"----------------------------------------------------------------------

  DATA: ls_goodsmvt_header TYPE bapi2017_gm_head_01,
        ls_goodsmvt_code   TYPE bapi2017_gm_code,
        lt_goodsmvt_item   TYPE STANDARD TABLE OF bapi2017_gm_item_create.
  DATA: lv_goodsmvt_headret TYPE bapi2017_gm_head_ret.
  DATA: lt_return           TYPE STANDARD TABLE OF bapiret2.
  DATA: lv_bktxt TYPE char40.
  DATA: lv_matnr TYPE matnr.
  DATA: lv_message TYPE char120.
  DATA: it_st02 TYPE STANDARD TABLE OF zhu_st02.
  DATA: it_st03 TYPE STANDARD TABLE OF zhu_st03.
  DATA: wa_st02 TYPE  zhu_st02.
  DATA: wa_st03 TYPE  zhu_st03.
  DATA: lv_hu(11)   TYPE c,
        lv_guid     TYPE guid_32,
        lv_msg(220) TYPE c.
  DATA: lt_save_log TYPE STANDARD TABLE OF zhu_st01_log.

  SORT it_husave BY pstng_date plant.
  DATA: lt_lqua TYPE STANDARD TABLE OF zst_lqua.
  DATA: l_number   TYPE tanum.
  DATA: ls_st_pick TYPE zst_pick.

  IF im_picnr IS INITIAL.
    ex_message = 'Blank Picklist is not allowed.'.
    RETURN.
  ENDIF.

  SELECT * FROM zst_pick
          INTO TABLE @DATA(lt_st_pick)
           WHERE picnr EQ @im_picnr
       AND lgnum = 'SDC'
       AND werks EQ @im_site
       AND lgpla EQ @im_bin
       AND lgtyp EQ @im_site+1(3).

  PERFORM save_temp_hst_picking TABLES it_husave
                                       lt_lqua
                                       imt_hu_status
                                USING  l_number
                                       im_picnr
                                       im_bin.

  SELECT * FROM zwm_store_hst INTO TABLE @DATA(lt_store)
    WHERE lgnum = 'SDC' AND
          hst_nr = @l_number.

  CALL FUNCTION 'DEQUEUE_ALL'
    EXPORTING
      _synchron = 'X'.

  LOOP AT it_husave INTO DATA(wa_final) WHERE stge_loc = '0008' .
    APPEND INITIAL LINE TO lt_goodsmvt_item ASSIGNING FIELD-SYMBOL(<fs_item>).
    <fs_item>-material   = |{ wa_final-article ALPHA = IN }|.
    <fs_item>-plant      = wa_final-plant.
    <fs_item>-stge_loc   = '0002'.
    <fs_item>-move_type  = '311'.
    <fs_item>-entry_qnt  = wa_final-scan_qty.
    <fs_item>-move_stloc = |{ wa_final-stge_loc ALPHA = IN }|.
    lv_hu = wa_final-hu+9(11).
    CONCATENATE lv_hu '-0002 to 0008' INTO lv_bktxt.
    ls_goodsmvt_header-pstng_date = sy-datum."wa_final-pstng_date.
    ls_goodsmvt_header-doc_date   = sy-datum.
    ls_goodsmvt_header-header_txt = lv_bktxt.
  ENDLOOP.

  REFRESH lt_return.

  IF lt_goodsmvt_item IS NOT INITIAL.
    CALL FUNCTION 'BAPI_GOODSMVT_CREATE'
      EXPORTING
        goodsmvt_header  = ls_goodsmvt_header
        goodsmvt_code    = '04'
      IMPORTING
        materialdocument = ex_mat_no
        matdocumentyear  = ex_year
      TABLES
        goodsmvt_item    = lt_goodsmvt_item
        return           = lt_return.
  ENDIF.

  IF ex_mat_no IS NOT INITIAL OR lt_goodsmvt_item IS INITIAL.
    CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'
      EXPORTING
        wait = 'X'.
    ex_save = 'X'.

    LOOP AT lt_store ASSIGNING FIELD-SYMBOL(<lfs_store>).
      IF <lfs_store>-doc_type = 'W'.
        <lfs_store>-mblnr = ex_mat_no.
        <lfs_store>-mjahr = ex_year.
      ENDIF.
    ENDLOOP.
    UNASSIGN <lfs_store>.
*    UPDATE zwm_store_hst SET : mblnr = ex_mat_no
*                               mjahr = ex_year
*                        WHERE hst_nr = l_number
*                          AND doc_type = 'W'.
  ELSE.
    CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
    LOOP AT lt_return INTO DATA(wa_return) WHERE type = 'E'.
      ex_to_return-message = wa_return-message.
    ENDLOOP.

    IF sy-uname EQ 'BATCHUSER'.
      READ TABLE lt_return INTO wa_return WITH KEY type = 'E'.
      IF sy-subrc = 0.
        lv_msg = wa_return-message.
        CONCATENATE 'Error:' lv_msg INTO lv_message.
        ex_message = lv_message.
      ENDIF.
    ELSE.
      MESSAGE ex_to_return-message TYPE 'E' DISPLAY LIKE 'I'.
    ENDIF.

    LOOP AT lt_store ASSIGNING <lfs_store>.
      IF <lfs_store>-doc_type = 'W'.
        <lfs_store>-msg = ex_to_return-message.
      ENDIF.
    ENDLOOP.
    UNASSIGN <lfs_store>.
*
*    UPDATE zwm_store_hst SET : msg = ex_to_return-message
*                        WHERE hst_nr = l_number
*                          AND doc_type = 'W'.

    EXIT.
  ENDIF.

  CALL FUNCTION 'DEQUEUE_ALL'
    EXPORTING
      _synchron = 'X'.

  DATA:lv_lgort TYPE lgort_d,
       lv_tbnum TYPE tbnum,
       lt_trite TYPE  l03b_trite_t.
  lv_lgort = '0008'.

  IF ex_mat_no IS NOT INITIAL OR lt_goodsmvt_item IS INITIAL.
    CONCATENATE ' Material Doc-'  ex_mat_no INTO lv_message.
*    PERFORM f_fill_trite USING ex_mat_no ex_year lv_tbnum it_husave lt_trite lv_lgort.
    LOOP AT it_husave ASSIGNING FIELD-SYMBOL(<lwa_husave>).
      READ TABLE imt_lqua ASSIGNING FIELD-SYMBOL(<lwa_lqau>)
              WITH KEY matnr = <lwa_husave>-article.
      IF sy-subrc IS INITIAL.
        APPEND INITIAL LINE TO lt_lqua ASSIGNING FIELD-SYMBOL(<lwa1>).
        <lwa1> = CORRESPONDING #( <lwa_lqau> ).
      ENDIF.
    ENDLOOP.

    CLEAR: it_ltap_create, it_ltap_c0002.
    PERFORM f_fill_data TABLES it_husave lt_lqua USING im_bin it_ltap_create .
    PERFORM f_fill_data_0002 TABLES it_husave lt_lqua USING im_bin it_ltap_c0002 .

    IF it_ltap_create IS NOT INITIAL.
      PERFORM f_l_to_create_tr USING it_ltap_create ex_tanum  ex_to_return.
      IF ex_tanum IS NOT INITIAL.

        LOOP AT lt_store ASSIGNING <lfs_store>.
          IF <lfs_store>-doc_type = 'W'.
            <lfs_store>-tanum = ex_tanum.
            CLEAR <lfs_store>-msg.
          ENDIF.
        ENDLOOP.
        UNASSIGN <lfs_store>.

*        UPDATE zwm_store_hst SET : tanum = ex_tanum
*                                   msg = ''
*                             WHERE hst_nr = l_number
*                               AND doc_type = 'W'.
        ex_save = 'X'.
        ex_to_return-message_v1 = ex_tanum.
        CONCATENATE lv_message '| TO no.-' ex_tanum INTO lv_message.
*      ENDIF.
      ELSE.

        LOOP AT lt_store ASSIGNING <lfs_store>.
          IF <lfs_store>-doc_type = 'W'.
            <lfs_store>-msg = ex_to_return-message.
          ENDIF.
        ENDLOOP.
        UNASSIGN <lfs_store>.

*      UPDATE zwm_store_hst SET : msg = ex_to_return-message
*                       WHERE hst_nr = l_number
*                     AND doc_type = 'W'.

*      ex_to_return-message = 'No bin exists'.
      ENDIF.
    ENDIF.

    IF it_ltap_c0002 IS NOT INITIAL.
      PERFORM f_l_to_create_tr USING it_ltap_c0002 ex_tanum_bin  ex_to_return.
      IF ex_tanum_bin IS NOT INITIAL.

        LOOP AT lt_store ASSIGNING <lfs_store>.
          IF <lfs_store>-doc_type = 'X'.
            <lfs_store>-mblnr = '9999999999'.
            <lfs_store>-mjahr = '9999'.
            <lfs_store>-tanum = ex_tanum_bin.
            CLEAR <lfs_store>-msg.
          ENDIF.
        ENDLOOP.
        UNASSIGN <lfs_store>.

*        UPDATE zwm_store_hst SET : mblnr = '9999999999'
*                                   mjahr = '9999'
*                                   tanum = ex_tanum_bin
*                                   msg = ''
*                             WHERE hst_nr = l_number
*                               AND doc_type = 'X'.
        ex_to_return-message_v2 = ex_tanum_bin.
        ex_save = 'X'.
        CONCATENATE lv_message '| Bin TO no.-' ex_tanum_bin INTO lv_message.
*      ENDIF.
      ELSE.

        LOOP AT lt_store ASSIGNING <lfs_store>.
          IF <lfs_store>-doc_type = 'X'.
            <lfs_store>-msg = ex_to_return-message.
          ENDIF.
        ENDLOOP.
        UNASSIGN <lfs_store>.
*
*      UPDATE zwm_store_hst SET : msg = ex_to_return-message
*                           WHERE hst_nr = l_number
*                             AND doc_type = 'X'.
*      ex_to_return-message = 'No bin exists'.
      ENDIF.
    ENDIF.
  ENDIF.

  CLEAR: wa_final, ls_goodsmvt_header.
  CLEAR: lt_goodsmvt_item,lt_goodsmvt_item.

  IF im_complete_flag EQ abap_true.
*    LOOP AT imt_hu_status INTO DATA(wa_final1).
*      APPEND INITIAL LINE TO lt_goodsmvt_item ASSIGNING <fs_item>.
*      <fs_item>-material = |{ wa_final1-matnr ALPHA = IN }|.
*      <fs_item>-plant = wa_final1-werks.
*      <fs_item>-stge_loc = '0002'.
*      <fs_item>-move_type = '311'.
*      <fs_item>-entry_qnt = wa_final1-req_0008.
*      <fs_item>-move_stloc = '0003'.
**      lv_bktxt = 'HUPutWay 0002 to 0003'.
*      lv_hu = wa_final1-hu_no+9(11).
*      CONCATENATE lv_hu '-0002 to 0003' INTO lv_bktxt.
*      ls_goodsmvt_header-pstng_date = sy-datum.
*      ls_goodsmvt_header-doc_date   = sy-datum.
*      ls_goodsmvt_header-header_txt = lv_bktxt.
*    ENDLOOP.
*    DELETE lt_goodsmvt_item WHERE entry_qnt = ''.
*    IF lt_goodsmvt_item[] IS NOT INITIAL.
*      REFRESH lt_return.
*      CALL FUNCTION 'BAPI_GOODSMVT_CREATE'
*        EXPORTING
*          goodsmvt_header  = ls_goodsmvt_header
*          goodsmvt_code    = '04'
*        IMPORTING
*          materialdocument = ex_mat_no
*          matdocumentyear  = ex_year
*        TABLES
*          goodsmvt_item    = lt_goodsmvt_item
*          return           = lt_return.
*
*      IF ex_mat_no IS NOT INITIAL .
*        CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'
*          EXPORTING
*            wait = 'X'.
*        ex_save = 'X'.
*        UPDATE zwm_store_hst SET: mblnr = ex_mat_no
*                                  mjahr = ex_year
*                            WHERE hst_nr = l_number
*                              AND doc_type = 'K'.
*        CONCATENATE lv_message ' Material Doc-'  ex_mat_no INTO lv_message.
*      ELSE.
*        CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
*        LOOP AT lt_return INTO wa_return WHERE type = 'E'.
*          ex_to_return-message = wa_return-message.
*        ENDLOOP.
**      IF sy-uname NE 'BATCHUSER'.
**        MESSAGE ex_to_return-message TYPE 'E' DISPLAY LIKE 'I'.
**      ENDIF.
*
*        IF sy-uname EQ 'BATCHUSER'.
*          READ TABLE lt_return INTO wa_return WITH KEY type = 'E'.
*          IF sy-subrc = 0.
*            lv_msg = wa_return-message.
*            CONCATENATE lv_message 'Error:' lv_msg INTO lv_message.
*            ex_message = lv_message.
*          ENDIF.
*        ELSE.
*          MESSAGE ex_to_return-message TYPE 'E' DISPLAY LIKE 'I'.
*        ENDIF.
*
*        UPDATE zwm_store_hst SET : msg = ex_to_return-message
*                             WHERE hst_nr = l_number
*                               AND doc_type = 'K'.
*        EXIT.
*      ENDIF.
*
*      CALL FUNCTION 'DEQUEUE_ALL'
*        EXPORTING
*          _synchron = 'X'.
*
*      CLEAR: wa_final, ls_goodsmvt_header.
*      CLEAR: lt_goodsmvt_item,lt_goodsmvt_item.

    " Hu Completed.
    CLEAR it_ltap_create[].
    PERFORM f_hu_comp TABLES imt_hu_status imt_lqua
                      USING im_bin it_ltap_create .
    IF it_ltap_create IS NOT INITIAL.
      CLEAR: ex_tanum.
      PERFORM f_l_to_create_tr USING it_ltap_create ex_tanum  ex_to_return.
      IF ex_tanum IS NOT INITIAL.

        LOOP AT lt_store ASSIGNING <lfs_store>.
          IF <lfs_store>-doc_type = 'Y'.
            <lfs_store>-mblnr = '9999999999'.
            <lfs_store>-mjahr = '9999'.
            <lfs_store>-tanum = ex_tanum.
            CLEAR <lfs_store>-msg.
          ENDIF.
        ENDLOOP.
        UNASSIGN <lfs_store>.

*        UPDATE zwm_store_hst SET: mblnr = '9999999999'
*                                  mjahr = '9999'
*                                  tanum = ex_tanum
*                                  msg = ''
*                            WHERE hst_nr = l_number
*                              AND doc_type = 'Y'.

        ex_to_return-message_v3 = ex_tanum.
        CONCATENATE lv_message '| Comp TO no.-' ex_tanum INTO lv_message.
      ELSE.
        LOOP AT lt_store ASSIGNING <lfs_store>.
          IF <lfs_store>-doc_type = 'Y'.
            <lfs_store>-msg = ex_to_return-message.
          ENDIF.
        ENDLOOP.
        UNASSIGN <lfs_store>.

      ENDIF.
*    ELSE.
*
*      LOOP AT lt_store ASSIGNING <lfs_store>.
*        IF <lfs_store>-doc_type = 'Y'.
*          <lfs_store>-msg = ex_to_return-message.
*        ENDIF.
*      ENDLOOP.
*      UNASSIGN <lfs_store>.
**
**      UPDATE zwm_store_hst SET: msg = ex_to_return-message
**                          WHERE hst_nr = l_number
**                            AND doc_type = 'Y'.
**        ex_to_return-message = 'No bin exists'.
*    ENDIF.
    ENDIF.
  ENDIF.

  SORT: lt_st_pick BY matnr,
        imt_hu_status BY matnr.
  LOOP AT imt_hu_status ASSIGNING FIELD-SYMBOL(<lfs_hu>).
    ASSIGN lt_st_pick[ matnr = <lfs_hu>-matnr ] TO FIELD-SYMBOL(<lfs_st>).

    IF <lfs_st> IS ASSIGNED.
      <lfs_st>-picked_qty = <lfs_hu>-scan_qty + <lfs_hu>-req_0008 + <lfs_hu>-bin_qty.
    ENDIF.
    UNASSIGN <lfs_st>.
  ENDLOOP.

*    READ TABLE lt_st_pick INTO ls_st_pick
*                          WITH KEY matnr = wa_final2-matnr.
*
*    IF sy-subrc IS INITIAL.
*      ls_st_pick-picked_qty = wa_final2-scan_qty + wa_final2-req_0008 + wa_final2-bin_qty.
*      MODIFY lt_st_pick FROM ls_st_pick.
*    ENDIF.
*
*  ENDLOOP.
*
  IF lt_store IS NOT INITIAL.
    MODIFY zwm_store_hst FROM TABLE lt_store[].
  ENDIF.

  IF lt_st_pick IS NOT INITIAL.
    MODIFY zst_pick FROM TABLE lt_st_pick[].
  ENDIF.
*  if lt_save_log[] is not initial.
*    loop at lt_save_log assigning <lwa_save>.
*      <lwa_save>-mblnr_comp = ex_mat_no.
*      <lwa_save>-mjahr_comp = ex_year.
*      <lwa_save>-tanum_comp = ex_tanum.
*    endloop.
*    modify zhu_st01_log from table lt_save_log[].
*  endif.

  ex_message = lv_message.

ENDFUNCTION.