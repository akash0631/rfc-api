FUNCTION zwm_store_bin_list_validation.
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
BREAK-POINT ID Z_V2CHECK.
  DATA :
        lt_lqua TYPE STANDARD TABLE OF lqua,
        lt_st_pick TYPE STANDARD TABLE OF zst_bin_cons.

  DATA : ls_pick TYPE zst_bin_cons ,
         ls_picklist TYPE zwm_store_stru,
         ls_lqua TYPE lqua.

  DATA:
      l_picnr TYPE zpicnr,
      lv_req_q TYPE menge_d.

  FIELD-SYMBOLS :
                 <lfs_pick> TYPE zst_bin_cons.

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


  SELECT * FROM zst_bin_cons
          INTO  TABLE lt_st_pick
           WHERE picnr EQ l_picnr
       AND lgnum EQ 'SDC'
       AND werks EQ im_werks
       AND lgort EQ im_lgort
       AND lgtyp EQ im_werks+1(3)
       and exidv = ''
       AND elikz = ''.
  IF sy-subrc IS  NOT INITIAL.
    ex_return-type = c_error.
    ex_return-message = 'Invalid Picklist'.
    RETURN .
  ELSE.

*****  check avaialable stock
*    SELECT * FROM lqua
*            INTO TABLE lt_lqua
*            FOR ALL ENTRIES IN lt_st_pick
*            WHERE  lgnum EQ im_lgnum
*       AND werks EQ im_werks
*       AND lgort EQ im_lgort
*       AND lgtyp EQ im_werks+1(3)
*       AND lgpla = lt_st_pick-lgpla.
*
*    SORT lt_lqua BY matnr lgpla.

  ENDIF.


  LOOP AT  lt_st_pick ASSIGNING <lfs_pick>.

    ls_picklist-picnr = <lfs_pick>-picnr.
    ls_picklist-plant = <lfs_pick>-werks.
    ls_picklist-material = <lfs_pick>-matnr.
    ls_picklist-bin = <lfs_pick>-lgpla.
    ls_picklist-stor_loc = <lfs_pick>-lgort.
    ls_picklist-storage_type = <lfs_pick>-lgtyp.
    ls_picklist-avl_stock = <lfs_pick>-verme.
*    ls_picklist-pick_qty = <lfs_pick>-verme.
    APPEND ls_picklist TO et_picklist .
    CLEAR ls_picklist .

  ENDLOOP.
BREAK-POINT id Z_V2CHECK.
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