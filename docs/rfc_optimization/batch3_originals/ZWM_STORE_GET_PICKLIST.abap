FUNCTION zwm_store_get_picklist.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_PICNR) TYPE  ZPICNR
*"     VALUE(IM_LGORT) TYPE  LGORT_D DEFAULT '0002'
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_EAN_DATA STRUCTURE  MARM OPTIONAL
*"      ET_PICKLIST TYPE  ZWM_STORE_STRU_T
*"----------------------------------------------------------------------

  DATA: lt_lqua    TYPE STANDARD TABLE OF lqua,
        lt_st_pick TYPE STANDARD TABLE OF zst_pick.

  DATA: ls_pick     TYPE zst_pick,
        ls_picklist TYPE zwm_store_stru,
        ls_lqua     TYPE lqua.

  DATA: l_picnr  TYPE zpicnr,
        lv_req_q TYPE menge_d.

  FIELD-SYMBOLS: <lfs_pick> TYPE zst_pick.

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

  SELECT SINGLE * FROM zwm_st_active
          INTO @data(gs_st_active)
          WHERE lgnum = 'SDC'
            AND werks = @im_werks.

  IF sy-subrc IS INITIAL.
    IF gs_st_active-pick11 IS INITIAL .
      ex_return-message  = 'Picklist not allowed'.
    ex_return-type = 'E'.
    EXIT.
    ENDIF.
  ELSE.
    ex_return-message  = 'Entry not maintained in zsdc_st_allow'.
    ex_return-type = 'E'.
    EXIT.
  ENDIF.

  IF im_picnr IS INITIAL .
    ex_return-message  = 'Blank Picklist not allowed'.
    ex_return-type = 'E'.
    EXIT.
  ENDIF.

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
       AND lgort EQ im_lgort
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
    SELECT * FROM lqua
            INTO TABLE lt_lqua
            FOR ALL ENTRIES IN lt_st_pick
            WHERE  lgnum EQ im_lgnum
       AND werks EQ im_werks
       AND lgort EQ im_lgort
       AND lgtyp EQ im_werks+1(3)
       AND lgpla = lt_st_pick-lgpla.

    SORT lt_lqua BY matnr lgpla.
    LOOP AT lt_st_pick ASSIGNING <lfs_pick>.
      <lfs_pick>-verme = ''.

      READ TABLE lt_lqua INTO ls_lqua WITH KEY matnr = <lfs_pick>-matnr
                                               lgpla = <lfs_pick>-lgpla
                                               BINARY SEARCH .
      IF sy-subrc IS INITIAL .
        lv_req_q =  <lfs_pick>-pick_qty -  <lfs_pick>-picked_qty .

        IF lv_req_q LE ls_lqua-verme .
          <lfs_pick>-verme = lv_req_q.
        ELSE.
          <lfs_pick>-verme = ls_lqua-verme.
        ENDIF.
      ELSE.
        <lfs_pick>-werks = ''.
      ENDIF.
    ENDLOOP.
    DELETE lt_st_pick WHERE werks IS INITIAL.
    DELETE lt_st_pick WHERE verme LT 0.
  ENDIF.

  IF lt_st_pick IS NOT INITIAL.
    SELECT matnr matkl
     FROM mara
     INTO TABLE lt_mara
     FOR ALL ENTRIES IN lt_st_pick
     WHERE matnr = lt_st_pick-matnr.
    IF sy-subrc = 0.
      SELECT matkl wgbez
        FROM t023t
        INTO TABLE lt_t023t
        FOR ALL ENTRIES IN lt_mara
        WHERE matkl = lt_mara-matkl.
    ENDIF.
  ENDIF.

  LOOP AT  lt_st_pick ASSIGNING <lfs_pick>.
    ls_picklist-picnr = <lfs_pick>-picnr.
    ls_picklist-plant = <lfs_pick>-werks.
    ls_picklist-material = <lfs_pick>-matnr.
    ls_picklist-bin = <lfs_pick>-lgpla.
    ls_picklist-stor_loc = <lfs_pick>-lgort.
    ls_picklist-storage_type = <lfs_pick>-lgtyp.
    ls_picklist-avl_stock = <lfs_pick>-verme.
    ls_picklist-pick_qty = <lfs_pick>-verme.
    READ TABLE lt_mara INTO ls_mara WITH KEY matnr = <lfs_pick>-matnr.
    IF sy-subrc = 0.
      READ TABLE lt_t023t INTO ls_t023t WITH KEY matkl = ls_mara-matkl.
      IF sy-subrc = 0.
        ls_picklist-matkl = ls_t023t-matkl.
        ls_picklist-wgbez = ls_t023t-wgbez.
      ENDIF.
    ENDIF.

    APPEND ls_picklist TO et_picklist .
    CLEAR ls_picklist .

  ENDLOOP.
*  SELECT picnr
*         lgnum AS wm_no
*         werks AS plant
*         matnr AS material
*         lgpla AS bin
*         lgort AS stor_loc
*         lgtyp AS storage_type
*         pick_qty AS avl_stock
*         pick_qty
*    FROM zst_pick
*    INTO CORRESPONDING FIELDS OF TABLE et_picklist
*   WHERE picnr EQ l_picnr
*     AND lgnum EQ im_lgnum
*     AND werks EQ im_werks
*     AND lgort EQ im_lgort
*     AND lgtyp EQ im_werks+1(3).
  BREAK-POINT ID z_v2check.

  IF et_picklist[]  IS NOT INITIAL.

    CALL FUNCTION 'ZWM_STORE_GET_EAN_DATA'
      EXPORTING
        it_data     = et_picklist[]
      TABLES
        et_ean_data = et_ean_data.
*    IF im_werks EQ 'HD22'.
*      DELETE et_ean_data WHERE meinh = 'PAK'.
*    ENDIF .
  ELSE.

    ex_return-type = c_error.
    ex_return-message = 'No Stock Data found'.

  ENDIF.
ENDFUNCTION.