FUNCTION ZWM_STORE_TRANSFER_0008_TO_001.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGORT_SRC) TYPE  LGORT_D DEFAULT '0008'
*"     VALUE(IM_LGORT_DEST) TYPE  LGORT_D DEFAULT '0001'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"     VALUE(EX_MBLNR) TYPE  MBLNR
*"     VALUE(EX_MJAHR) TYPE  MJAHR
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T
*"----------------------------------------------------------------------
BREAK-POINT ID Z_V2CHECK.
  DATA:
      lt_data    TYPE ty_t_data,
      lr_data    TYPE REF TO zwm_store_stru,
      lr_data2   TYPE REF TO ty_data.

  DATA:
      l_matnr    TYPE matnr,
      l_exidv    TYPE exidv,
      l_tabix    TYPE sytabix,
      l_number   TYPE numc10,
      l_doc_type TYPE char1,
      l_rc       TYPE sysubrc.

  DATA:
      lt_hu     TYPE hum_hu_item_t,
      lt_hdr    TYPE hum_hu_header_t.
  DATA:
      lr_hu     TYPE REF TO vepovb.

  DATA:
     lt_ltap_create TYPE STANDARD TABLE OF ltap_creat      INITIAL SIZE 0,
     lt_ltap        TYPE STANDARD TABLE OF ltap_vb         INITIAL SIZE 0,
     lt_vekp        TYPE STANDARD TABLE OF vekp            INITIAL SIZE 0.

  DATA:
     ls_ltap_create TYPE ltap_creat,
     ls_vekp        TYPE vekp,
     l_tanum        TYPE tanum.

  DATA:
     ls_husdc TYPE zmm_husdc,
     lt_husdc TYPE STANDARD TABLE OF zmm_husdc INITIAL SIZE 0.

  DATA:
      lv_count TYPE mblpo.

  DATA:
      l_vgbel  TYPE vgbel.

  DATA:
     l_mblnr  TYPE mblnr,
     l_mjahr  TYPE mjahr,
     l_tanum2 TYPE tanum,
     lv_dloc  TYPE lgort_d,
     l_user   TYPE wwwobjid.


  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_user
    IMPORTING
      output = l_user.

  LOOP AT it_data REFERENCE INTO lr_data.

* Convert into internal format
    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        input  = lr_data->material
      IMPORTING
        output = l_matnr.

    READ TABLE lt_data REFERENCE INTO lr_data2
    WITH KEY matnr = l_matnr
             lgpla = lr_data->bin.

    IF sy-subrc IS NOT INITIAL.
      APPEND INITIAL LINE TO lt_data REFERENCE INTO lr_data2.
      MOVE:
        l_matnr        TO lr_data2->matnr,
        lr_data->bin   TO lr_data2->lgpla.
    ENDIF.

    lr_data2->menge = lr_data2->menge + lr_data->scan_qty.

  ENDLOOP.

  PERFORM f_transfer_stock USING im_lgnum
                                 im_werks
                                 im_lgort_src
                                 im_lgort_dest
                                 lt_data
                                 l_number
                                 l_doc_type
                        CHANGING ex_mblnr
                                 ex_mjahr
                                 ex_tanum
                                 ex_return.

ENDFUNCTION.