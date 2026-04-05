FUNCTION zwm_store_hu_grc.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_MBLNR) TYPE  MBLNR
*"     VALUE(EX_MJAHR) TYPE  MJAHR
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------
BREAK-POINT id Z_V2CHECK.
  DATA : ls_hst TYPE zwm_store_hst ,
          lt_hst TYPE STANDARD TABLE OF zwm_store_hst ,
          lt_exref  TYPE STANDARD TABLE OF zwm_exref,
          lt_st_hst TYPE STANDARD TABLE OF zwm_store_hst .

  DATA :
         l_posnr  TYPE posnr,
         lv_number TYPE numc10.
  DATA:
     lr_data  TYPE REF TO zwm_store_stru.
*     lt_vekp  TYPE STANDARD TABLE OF vekp INITIAL SIZE 0,
*     lt_store TYPE STANDARD TABLE OF zwm_store INITIAL SIZE 0,
*     lr_vekp  TYPE REF TO vekp,
*     ls_store TYPE zwm_store,
*     lt_items TYPE bapi2017_gm_item_create_t.

  DATA:
     l_user   TYPE wwwobjid,
     ls_exref TYPE zwm_exref,
     ls_msg   TYPE bdcmsgcoll.

*  DATA:
*     ls_goodsmvt_code    TYPE bapi2017_gm_code ,
*     ls_goodsmvt_header  TYPE  bapi2017_gm_head_01,
*     lt_return           TYPE bapiret2_t,
*     ls_return           TYPE bapiret2.

*  CLEAR:
*    gt_messtab.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_user
    IMPORTING
      output = l_user.

*  ls_goodsmvt_code-gm_code      = '01' .
*  ls_goodsmvt_header-pstng_date = sy-datum .
*  ls_goodsmvt_header-doc_date   = sy-datum.
*  CONCATENATE l_user '-' 'HHT' INTO ls_goodsmvt_header-header_txt .

  IF it_data[] IS NOT INITIAL.



    SELECT * FROM zwm_store_hst
            INTO TABLE lt_st_hst
            FOR ALL ENTRIES IN it_data
             WHERE lgnum = 'SDC'
             AND exidv = it_data-hu_no
             AND werks = im_werks.

    SORT lt_st_hst BY exidv.
    PERFORM f_number_get_next USING lv_number .

    LOOP AT it_data REFERENCE INTO lr_data.

      CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
        EXPORTING
          input  = lr_data->hu_no
        IMPORTING
          output = lr_data->hu_no.

SELECT SINGLE exidv sap_hu vbeln datum
  FROM ZWM_EXREF INTO ls_exref where exidv = lr_data->hu_no.
      IF sy-subrc IS INITIAL AND ls_exref-sap_hu IS NOT INITIAL.
        ls_hst-exidv_ex = lr_data->hu_no.
        lr_data->hu_no = ls_exref-sap_hu.
      ENDIF.

      READ TABLE lt_st_hst WITH KEY exidv = lr_data->hu_no
                                    BINARY SEARCH
                                    TRANSPORTING NO FIELDS .
      IF sy-subrc IS NOT INITIAL.
        l_posnr = l_posnr + 1 .
        ls_hst-lgnum = 'SDC'.
        ls_hst-hst_nr = lv_number.
        ls_hst-posnr = l_posnr.
        ls_hst-doc_type =  'Z'.
        ls_hst-werks = im_werks.
        ls_hst-exidv = lr_data->hu_no.
        ls_hst-ernam = im_user.
        ls_hst-erdat = sy-datum.
        ls_hst-erzet = sy-uzeit.
        ls_hst-tcode = sy-tcode.
        APPEND ls_hst TO lt_hst .
      ENDIF.
      CLEAR ls_hst .
      clear ls_exref.
*      PERFORM build_move_item USING im_werks
*                                    lr_data->hu_no
*                           CHANGING lt_items.
    ENDLOOP.

    IF lt_hst[] IS NOT INITIAL.
      MODIFY zwm_store_hst FROM TABLE lt_hst.
      COMMIT WORK AND WAIT.
      ex_return-type = c_success.
*      CONCATENATE 'Document' ex_mblnr 'Posted Successfully'
*      INTO ex_return-message SEPARATED BY space.

      ex_return-message = 'Data Saved Check Document After some Time'.
    ELSE.
      ex_return-type = c_success.
*      CONCATENATE 'Document' ex_mblnr 'Posted Successfully'
*      INTO ex_return-message SEPARATED BY space.

      ex_return-message = 'Data already saved'.
    ENDIF.
  ENDIF.
*    SELECT *
*      FROM vekp
*      INTO TABLE lt_vekp
*      FOR ALL ENTRIES IN it_data
*      WHERE exidv EQ it_data-hu_no.
*
*    IF sy-subrc IS INITIAL.
**    Do Nothing
*    ENDIF.
*
*    CALL FUNCTION 'BAPI_GOODSMVT_CREATE'
*      EXPORTING
*        goodsmvt_header  = ls_goodsmvt_header
*        goodsmvt_code    = ls_goodsmvt_code
*      IMPORTING
*        materialdocument = ex_mblnr
*        matdocumentyear  = ex_mjahr
*      TABLES
*        goodsmvt_item    = lt_items
*        return           = lt_return.
*
*
*    IF ex_mblnr IS INITIAL.
*      READ TABLE lt_return INTO ls_return INDEX 1.
*
*      ex_return-type = ls_msg-msgtyp.
*
*      MESSAGE ID ls_return-id
*            TYPE ls_return-type
*          NUMBER ls_return-number
*            WITH ls_return-message_v1
*                 ls_return-message_v2
*                 ls_return-message_v3
*                 ls_return-message_v4
*                 INTO ex_return-message.
*
*    ELSE.
*
*      CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'.
*
*      ex_return-type = c_success.
*      CONCATENATE 'Document' ex_mblnr 'Posted Successfully'
*      INTO ex_return-message SEPARATED BY space.
*
*      LOOP AT lt_vekp REFERENCE INTO lr_vekp.
*        lr_vekp->vegr5  = 'HSTR'.
*        lr_vekp->vegr1  = im_werks.
*        lr_vekp->sealn5 = ex_mblnr.
*        ls_store-dwerks = im_werks.
*        ls_store-swerks = lr_vekp->werks.
*        ls_store-exidv  = lr_vekp->exidv.
*        ls_store-venum  = lr_vekp->venum.
*        ls_store-mblnr  = lr_vekp->sealn5.
*        ls_store-mjahr  = sy-datum.
*        ls_store-vbeln  = lr_vekp->vpobjkey.
*        ls_store-budat  = sy-datum.
*        ls_store-erdat  = sy-datum.
*        ls_store-ernam  = sy-uname.
*        ls_store-erzet  = sy-uzeit.
*        ls_store-ernam_rfc  = l_user.
*        APPEND ls_store TO lt_store .
*      ENDLOOP.
*
*      MODIFY vekp FROM TABLE lt_vekp.
*      MODIFY zwm_store FROM TABLE lt_store.
*
*
*    ENDIF.
*  ENDIF.

ENDFUNCTION.