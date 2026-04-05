FUNCTION zwm_store_hu_get_details.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_EXIDV) TYPE  EXIDV
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_VEMNG) TYPE  VEMNG
*"  TABLES
*"      ET_EAN_DATA STRUCTURE  MARM OPTIONAL
*"      ET_HU_HDR STRUCTURE  VEKPVB OPTIONAL
*"      ET_HU_ITEM STRUCTURE  VEPOVB OPTIONAL
*"      ET_LAGP STRUCTURE  LAGP OPTIONAL
*"----------------------------------------------------------------------
  BREAK-POINT ID z_v2check.
  DATA: l_exidv TYPE exidv,
        l_venum TYPE venum.

  DATA: lt_hu     TYPE hum_hu_item_t,
        lt_hdr    TYPE hum_hu_header_t,
        lt_hu311  TYPE STANDARD TABLE OF zmm_hu311 INITIAL SIZE 0,
        lr_hu     TYPE REF TO vepovb,
        lr_hu_itm TYPE REF TO vepovb,
        lr_hdr    TYPE REF TO vekpvb.

  DATA: lt_mean  TYPE STANDARD TABLE OF mean,
        lt_marm  TYPE STANDARD TABLE OF marm,
        lr_marm  TYPE REF TO marm,
        lr_mean  TYPE REF TO mean,
        lr_hu311 TYPE REF TO zmm_hu311,
        lr_ean   TYPE REF TO marm.

  DATA: l_rc    TYPE sysubrc,
        l_bdmng TYPE vemng,
        l_vbeln TYPE vbeln,
        l_tabix TYPE sytabix.

  CLEAR: et_ean_data[],et_hu_hdr,et_hu_item,ex_return.

*  ex_return-type = c_error.
*  ex_return-message = 'Full HU not allowed. Kindly put Article wise'.
*  RETURN.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_exidv
    IMPORTING
      output = l_exidv.

************************************************************
""""""""""""Added Select query by anuj on 08.05.2025""""""""
************************************************************
  SELECT SINGLE * FROM zwm_store_hst INTO @DATA(ls_zwm_store)
           WHERE exidv = @l_exidv
            AND  doc_type = '1'.
  IF sy-subrc IS INITIAL.
  ex_return = VALUE #( type = 'E' message = 'HU Already Scanned' ).
  ENDIF.
************************************************************
""""""""""""Select Query End""""""""""""""""""""""""""""""""
************************************************************

  PERFORM f_check_hu USING l_exidv im_werks l_vbeln CHANGING ex_return l_rc.
  CHECK l_rc IS INITIAL.

  CALL FUNCTION 'HU_GET_ONE_HU_DB'
    EXPORTING
      if_hu_number = l_exidv
    IMPORTING
      et_hu_header = lt_hdr
      et_hu_items  = lt_hu
    EXCEPTIONS
      hu_not_found = 1
      hu_locked    = 2
      fatal_error  = 3
      OTHERS       = 4.

  IF sy-subrc <> 0.
    ex_return-type = c_error.
    MESSAGE ID sy-msgid TYPE sy-msgty NUMBER sy-msgno
            WITH sy-msgv1 sy-msgv2 sy-msgv3 sy-msgv4
            INTO ex_return-message.
    RETURN.
  ENDIF.

  IF lt_hu IS NOT INITIAL.
    SELECT *
     FROM zmm_hu311
     INTO TABLE lt_hu311
     FOR ALL ENTRIES IN lt_hu
     WHERE venum = lt_hu-venum
       AND vepos = lt_hu-vepos.

    IF sy-subrc IS INITIAL.
      SORT lt_hu311 BY venum vepos.
    ENDIF.

    SELECT mandt matnr meinh umrez umren eannr ean11
      FROM marm
      INTO TABLE et_ean_data
      FOR ALL ENTRIES IN lt_hu
     WHERE matnr EQ lt_hu-matnr.

    IF sy-subrc IS INITIAL.
      lt_marm = et_ean_data[].
      SORT lt_marm BY matnr meinh.
    ENDIF.

    SELECT *
     FROM mean
     INTO TABLE lt_mean
     FOR ALL ENTRIES IN lt_hu
     WHERE matnr EQ lt_hu-matnr.

    IF sy-subrc IS INITIAL.
      LOOP AT lt_mean REFERENCE INTO lr_mean.
        READ TABLE et_ean_data REFERENCE INTO lr_ean WITH KEY ean11 = lr_mean->ean11.
        IF sy-subrc IS NOT INITIAL.
          APPEND INITIAL LINE TO et_ean_data REFERENCE INTO lr_ean.
          READ TABLE lt_marm REFERENCE INTO lr_marm WITH KEY matnr = lr_mean->matnr
                                                    meinh = lr_mean->meinh BINARY SEARCH.
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
    ENDIF.
  ENDIF.

  LOOP AT lt_hu REFERENCE INTO lr_hu.
    CLEAR: l_tabix.
    lr_hu->bdmng = lr_hu->vemng.

    READ TABLE lt_hu311 TRANSPORTING NO FIELDS WITH KEY venum = lr_hu->venum
                                               vepos = lr_hu->vepos BINARY SEARCH.
    IF sy-subrc IS INITIAL.
      l_tabix = sy-tabix.
      LOOP AT lt_hu311 REFERENCE INTO lr_hu311 FROM l_tabix.
        IF NOT ( lr_hu->venum EQ lr_hu311->venum AND lr_hu->vepos EQ lr_hu311->vepos ).
          EXIT.
        ENDIF.
        lr_hu->bdmng = lr_hu->bdmng - lr_hu311->menge.
      ENDLOOP.
    ENDIF.
  ENDLOOP.

  LOOP AT lt_hu REFERENCE INTO lr_hu.
    READ TABLE et_hu_item REFERENCE INTO lr_hu_itm WITH KEY venum = lr_hu->venum matnr = lr_hu->matnr.
    IF sy-subrc IS NOT INITIAL.
      APPEND INITIAL LINE TO et_hu_item REFERENCE INTO lr_hu_itm.
      lr_hu_itm->venum = lr_hu->venum.
      lr_hu_itm->matnr = lr_hu->matnr.
    ENDIF.

*lr_hu->vemng = lr_hu->vemng + 99 .
*lr_hu->bdmng = lr_hu->bdmng + 99.
    lr_hu_itm->vemng = lr_hu_itm->vemng + lr_hu->vemng .
    lr_hu_itm->bdmng = lr_hu_itm->bdmng + lr_hu->bdmng .

    ex_vemng = ex_vemng + lr_hu->vemng.
    l_bdmng = l_bdmng + lr_hu->bdmng.
  ENDLOOP.

  IF l_bdmng IS INITIAL.
    ex_return-type = c_error.
    MESSAGE i093(zwm) WITH im_exidv INTO ex_return-message.
    RETURN.
  ENDIF.

  CALL FUNCTION 'ZWM_STORE_GET_BIN'
    EXPORTING
      im_werks = im_werks
      im_lgnum = im_lgnum
    TABLES
      et_lagp  = et_lagp.

*Update Header Table
  et_hu_hdr[]  = lt_hdr.


ENDFUNCTION.