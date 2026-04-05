FUNCTION zwm_store_get_mat_from_ean.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_EAN) TYPE  EAN11 OPTIONAL
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_EAN_DATA STRUCTURE  MARM OPTIONAL
*"      ET_LQUA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------.
  DATA:
     lr_mean TYPE REF TO mean,
     lr_ean  TYPE REF TO marm,
     lr_marm TYPE REF TO marm,
     lt_marm TYPE STANDARD TABLE OF marm INITIAL SIZE 0,
     lt_lqua TYPE STANDARD TABLE OF lqua INITIAL SIZE 0 ,
     lt_mean TYPE STANDARD TABLE OF mean INITIAL SIZE 0.

  DATA :
         ls_data  TYPE zwm_store_stru,
         ls_lqua  TYPE lqua.

  IF im_ean IS INITIAL .
    ex_return-type = c_error.
    ex_return-message = 'No Blank EAN allow'.
    RETURN.
  ENDIF.


  SELECT *
   FROM mean
   INTO TABLE lt_mean
      WHERE ean11 = im_ean.
  IF sy-subrc IS NOT INITIAL.
    ex_return-type = c_error.
    ex_return-message = 'Invalid Barcode'.
    RETURN.
  ENDIF.


  SELECT mandt
         matnr
         meinh
         umrez
         umren
         eannr
         ean11
    FROM marm
    INTO TABLE et_ean_data
    FOR ALL ENTRIES IN lt_mean
   WHERE matnr = lt_mean-matnr.

  IF sy-subrc IS INITIAL.
    lt_marm = et_ean_data[].
    SORT lt_marm BY matnr meinh.
  ENDIF.
BREAK-POINT id Z_V2CHECK.
  SELECT lgnum matnr werks lgtyp lgpla lgort verme meins
    FROM LQUA
          INTO TABLE lt_lqua
            FOR ALL ENTRIES IN lt_mean
            WHERE lgnum = 'SDC'
              AND matnr = lt_mean-matnr
              AND werks = im_werks
              AND lgtyp = im_werks+1(3)
              AND lgort = '0001'.

  LOOP AT lt_lqua INTO ls_lqua .
    ls_data-wm_no = ls_lqua-lgnum.
    ls_data-material = ls_lqua-matnr.
    ls_data-bin = ls_lqua-lgpla.
    ls_data-plant = ls_lqua-werks.
    ls_data-avl_stock = ls_lqua-verme.
    COLLECT ls_data INTO et_lqua .
    CLEAR : ls_data , ls_lqua .
  ENDLOOP.

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
        MOVE lr_marm->* TO lr_ean->*.
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