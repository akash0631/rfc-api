FUNCTION zwm_store_picklist_bin .
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_PICNR) TYPE  ZPICNR
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_BIN) TYPE  LGPLA
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_FINAL TYPE  ZTT_PICK_BIN OPTIONAL
*"      ET_LQUA TYPE  ZST_LQUA_T OPTIONAL
*"      ET_EAN_DATA TYPE  ZST_PUT31_EAN_T OPTIONAL
*"----------------------------------------------------------------------
  DATA: lt_lqua    TYPE STANDARD TABLE OF lqua,
        lt_st_pick TYPE STANDARD TABLE OF zst_pick.
  DATA: et_picklist TYPE  zwm_store_stru_t.
  FIELD-SYMBOLS: <lfs_pick1> TYPE zwm_store_stru.

  DATA: ls_pick     TYPE zst_pick,
        ls_picklist TYPE zwm_store_stru,
        ls_lqua     TYPE lqua.

  DATA: l_picnr  TYPE zpicnr,
        lv_req_q TYPE menge_d.

  FIELD-SYMBOLS: <lfs_pick> TYPE zst_pick,
                 <lfs_lqua> TYPE zst_lqua.

  TYPES: BEGIN OF ty_mara,
           matnr TYPE matnr,
           matkl TYPE matkl,
         END OF ty_mara.

  TYPES: BEGIN OF ty_t023t,
           matkl TYPE matkl,
           wgbez TYPE wgbez,
         END OF ty_t023t.

  DATA: lt_mara  TYPE TABLE OF ty_mara,
        lt_t023t TYPE TABLE OF ty_t023t,
        ls_mara  TYPE ty_mara,
        ls_t023t TYPE ty_t023t.

  DATA:
    lr_mean TYPE REF TO mean,
    lr_ean  TYPE REF TO zst_put31_ean,
    lr_marm TYPE REF TO marm.
  FIELD-SYMBOLS : <lfs_marm> TYPE marm.

*  IF im_picnr IS INITIAL .
*    ex_return-message  = 'Blank Picklist not allowed'.
*    ex_return-type = 'E'.
*    EXIT.
*  ENDIF.
*
*  IF im_werks IS INITIAL .
*    ex_return-message  = 'Blank site not allowed'.
*    ex_return-type = 'E'.
*    EXIT.
*  ENDIF.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_picnr
    IMPORTING
      output = l_picnr.

  SELECT * FROM zst_pick
          INTO  TABLE lt_st_pick
           WHERE picnr EQ l_picnr
       AND lgnum EQ im_lgnum
       AND werks EQ im_werks
       AND lgpla EQ im_bin
       AND lgtyp EQ im_werks+1(3).
  IF sy-subrc IS  NOT INITIAL.
    ex_return-type = c_error.
    ex_return-message = 'Invalid Picklist'.
    RETURN .
  ELSE.
    LOOP AT lt_st_pick ASSIGNING <lfs_pick>.
      IF <lfs_pick>-pick_qty LE <lfs_pick>-picked_qty .
        <lfs_pick>-werks = ''.
      ENDIF.
    ENDLOOP.
    DELETE lt_st_pick WHERE werks IS INITIAL.

    IF lt_st_pick[] IS INITIAL.
      ex_return-type = c_error.
      ex_return-message = 'Picklist Closed'.
      RETURN .
    ENDIF.

*Check avaialable stock

    SELECT lgnum
           lqnum
           matnr
           werks
           lgpla
           verme
         FROM lqua INTO TABLE et_lqua
*         FOR ALL ENTRIES IN lt_st_pick
         WHERE lgnum = 'SDC'
           AND werks = im_werks
*           AND lgtyp = 'V01'
           AND lgpla = im_bin.
*           AND matnr = lt_st_pick-matnr.
    IF sy-subrc IS NOT INITIAL.
      ex_return-type = 'E'.
      ex_return-message = 'Stock is not available'.
      RETURN .
    ENDIF.

*    SELECT * FROM lqua
*            INTO TABLE lt_lqua
*            FOR ALL ENTRIES IN lt_st_pick
*            WHERE  lgnum EQ im_lgnum
*       AND werks EQ im_werks
**       AND lgort EQ im_lgort
*       AND lgtyp EQ im_werks+1(3)
*       AND lgpla = lt_st_pick-lgpla.
*
*    SORT lt_lqua BY matnr lgpla.
*    LOOP AT lt_st_pick ASSIGNING <lfs_pick>.
*      <lfs_pick>-verme = ''.
*
*      READ TABLE lt_lqua INTO ls_lqua WITH KEY matnr = <lfs_pick>-matnr
*                                               lgpla = <lfs_pick>-lgpla
*                                               BINARY SEARCH .
*      IF sy-subrc IS INITIAL .
*        lv_req_q =  <lfs_pick>-pick_qty -  <lfs_pick>-picked_qty .
*
*        IF lv_req_q LE ls_lqua-verme .
*          <lfs_pick>-verme = lv_req_q.
*        ELSE.
*          <lfs_pick>-verme = ls_lqua-verme.
*        ENDIF.
*      ELSE.
*        <lfs_pick>-werks = ''.
*      ENDIF.
*    ENDLOOP.
*    DELETE lt_st_pick WHERE werks IS INITIAL.
*    DELETE lt_st_pick WHERE verme LT 0.
  ENDIF.
*
*  IF lt_st_pick IS NOT INITIAL.
*    SELECT matnr matkl
*     FROM mara
*     INTO TABLE lt_mara
*     FOR ALL ENTRIES IN lt_st_pick
*     WHERE matnr = lt_st_pick-matnr.
*    IF sy-subrc = 0.
*      SELECT matkl wgbez
*        FROM t023t
*        INTO TABLE lt_t023t
*        FOR ALL ENTRIES IN lt_mara
*        WHERE matkl = lt_mara-matkl.
*    ENDIF.
*  ENDIF.

*  LOOP AT  lt_st_pick ASSIGNING <lfs_pick>.
*    ls_picklist-picnr = <lfs_pick>-picnr.
*    ls_picklist-plant = <lfs_pick>-werks.
*    ls_picklist-material = <lfs_pick>-matnr.
*    ls_picklist-bin = <lfs_pick>-lgpla.
*    ls_picklist-stor_loc = <lfs_pick>-lgort.
*    ls_picklist-storage_type = <lfs_pick>-lgtyp.
*    ls_picklist-avl_stock = <lfs_pick>-verme.
*    ls_picklist-pick_qty = <lfs_pick>-pick_qty.
*    ls_picklist-scan_qty = <lfs_pick>-picked_qty.
*    READ TABLE lt_mara INTO ls_mara WITH KEY matnr = <lfs_pick>-matnr.
*    IF sy-subrc = 0.
*      READ TABLE lt_t023t INTO ls_t023t WITH KEY matkl = ls_mara-matkl.
*      IF sy-subrc = 0.
*        ls_picklist-matkl = ls_t023t-matkl.
*        ls_picklist-wgbez = ls_t023t-wgbez.
*      ENDIF.
*    ENDIF.
*
*    APPEND ls_picklist TO et_picklist .
*    CLEAR ls_picklist .
*
*  ENDLOOP.
*  IF et_picklist[]  IS NOT INITIAL.
*
*    CALL FUNCTION 'ZWM_STORE_GET_EAN_DATA'
*      EXPORTING
*        it_data     = et_picklist[]
*      TABLES
*        et_ean_data = et_ean_data.
*  ELSE.
*    ex_return-type = c_error.
*    ex_return-message = 'No Stock Data found'.
*  ENDIF.

  UNASSIGN <lfs_pick>.
  LOOP AT lt_st_pick ASSIGNING <lfs_pick>.
    APPEND INITIAL LINE TO et_final ASSIGNING FIELD-SYMBOL(<lwa_final>).

    <lwa_final>-picnr        = <lfs_pick>-picnr.
*   <lwa_final>-hu_no        = <lfs_pick>-
    <lwa_final>-matnr        = <lfs_pick>-matnr.
    <lwa_final>-werks        = <lfs_pick>-werks.
    <lwa_final>-hu_qty       = <lfs_pick>-verme.
    <lwa_final>-picklist_qty = <lfs_pick>-pick_qty.
    <lwa_final>-scan_qty     = <lfs_pick>-picked_qty.
    <lwa_final>-req_0008     = <lfs_pick>-pick_qty - <lfs_pick>-picked_qty.
    <lwa_final>-bin_qty      = <lwa_final>-hu_qty - ( <lwa_final>-scan_qty + <lwa_final>-req_0008 ).
    <lwa_final>-bin          = <lfs_pick>-lgpla.
  ENDLOOP.
  UNASSIGN <lfs_lqua>.
  UNASSIGN <lwa_final>.
  SORT: et_lqua BY matnr,
        et_final BY matnr.
  LOOP AT et_lqua ASSIGNING <lfs_lqua>.
    READ TABLE et_final ASSIGNING <lwa_final> WITH KEY matnr = <lfs_lqua>-matnr.
*                                                       BINARY SEARCH.
    IF sy-subrc IS NOT INITIAL.
      APPEND INITIAL LINE TO et_final ASSIGNING FIELD-SYMBOL(<lwa_final1>).
      <lwa_final1>-picnr        = l_picnr.
*      <lwa_final1>-picnr        = im_picnr.
*         <lwa_final>-hu_no        = <lfs_pick>-
      <lwa_final1>-matnr        = <lfs_lqua>-matnr.
      <lwa_final1>-werks        = <lfs_lqua>-werks.
      <lwa_final1>-hu_qty       = <lfs_lqua>-verme.
      <lwa_final1>-picklist_qty = '0'.
      <lwa_final1>-scan_qty     = '0'.
      <lwa_final1>-req_0008     = <lfs_lqua>-verme.
      <lwa_final1>-bin_qty      = <lfs_lqua>-verme.
      <lwa_final1>-bin          = <lfs_lqua>-lgpla.
    ENDIF.

  ENDLOOP.
  IF et_final[] IS NOT INITIAL.
    SELECT * FROM mean INTO TABLE @DATA(lt_mean)
             FOR ALL ENTRIES IN @et_final
             WHERE matnr = @et_final-matnr.
  ENDIF.


  SELECT *
       FROM marm
       INTO TABLE @DATA(lt_marm)
       FOR ALL ENTRIES IN @lt_mean
         WHERE matnr = @lt_mean-matnr.
  IF lt_marm[] IS NOT INITIAL.
    SORT lt_marm BY matnr meinh.
  ENDIF.
  LOOP AT lt_mean REFERENCE INTO lr_mean.
    READ TABLE et_ean_data REFERENCE INTO lr_ean
    WITH KEY ean11 = lr_mean->ean11.
    IF sy-subrc IS NOT INITIAL.
      APPEND INITIAL LINE TO et_ean_data REFERENCE INTO lr_ean.
      READ TABLE lt_marm REFERENCE INTO lr_marm
      WITH KEY matnr = lr_mean->matnr
               meinh = lr_mean->meinh
               BINARY SEARCH.
      IF sy-subrc IS INITIAL.
*        MOVE lr_marm->* TO lr_ean->*.
        lr_ean->umrez = lr_marm->umrez.
        lr_ean->umren = lr_marm->umren.
        lr_ean->matnr = lr_mean->matnr.
        lr_ean->ean11 = lr_mean->ean11.
      ELSE.
        MOVE-CORRESPONDING lr_mean->* TO lr_ean->*.
        lr_ean->umrez = 1.
        lr_ean->umren = 1.
      ENDIF.

    ENDIF.
  ENDLOOP.

  DELETE et_ean_data WHERE ean11 IS INITIAL.

ENDFUNCTION.