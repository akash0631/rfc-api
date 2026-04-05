FUNCTION zwm_store_get_v01_001s_stock.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"      ET_EAN_DATA STRUCTURE  MARM OPTIONAL
*"      ET_LQUA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------
  DATA: lt_lqua TYPE STANDARD TABLE OF lqua,
        ls_lqua TYPE lqua,
        ls_data  TYPE zwm_store_stru.
BREAK-POINT id Z_V2CHECK.
  SELECT * FROM lqua
    INTO TABLE lt_lqua
    WHERE lgnum = 'SDC'
      AND werks = im_werks
      AND lgtyp = 'V01'
      AND lgpla LIKE '49%'
      AND lgort = '0001'
      %_HINTS ORACLE '&SUBSTITUTE VALUES&'.

  LOOP AT lt_lqua INTO ls_lqua.
    ls_data-wm_no     = ls_lqua-lgnum.
    ls_data-material  = ls_lqua-matnr.
    ls_data-plant     = ls_lqua-werks.
    ls_data-avl_stock = ls_lqua-verme.

    COLLECT ls_data INTO et_data.
    CLEAR: ls_data, ls_lqua.
  ENDLOOP.
BREAK-POINT id Z_V2CHECK.
  SORT et_data BY material.
  IF et_data[] IS INITIAL.
    ex_return-message  = 'No Data For Putway'.
    ex_return-type = 'E'.
    EXIT.
  ENDIF.

  REFRESH lt_lqua.
  SELECT * FROM lqua
    INTO TABLE lt_lqua
    FOR ALL ENTRIES IN et_data
    WHERE lgnum = 'SDC'
      AND matnr = et_data-material
      AND werks = im_werks
      AND lgtyp = '122' "im_werks+1(3) VKS-11.03.2021
      AND lgort = '0001'
      %_HINTS ORACLE '&SUBSTITUTE VALUES&'.

  LOOP AT lt_lqua INTO ls_lqua.
    ls_data-wm_no = ls_lqua-lgnum.
    ls_data-material = ls_lqua-matnr.
    ls_data-bin = ls_lqua-lgpla.
    ls_data-plant = ls_lqua-werks.
    ls_data-avl_stock = ls_lqua-verme.

    COLLECT ls_data INTO et_lqua.
    CLEAR: ls_data, ls_lqua.
  ENDLOOP.

*EAN Data
  CALL FUNCTION 'ZWM_STORE_GET_EAN_DATA'
    EXPORTING
      it_data     = et_data[]
    TABLES
      et_ean_data = et_ean_data.

  DELETE et_ean_data WHERE meinh = 'PAK'.
ENDFUNCTION.