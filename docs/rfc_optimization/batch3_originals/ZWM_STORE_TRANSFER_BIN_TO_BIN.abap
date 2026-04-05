FUNCTION zwm_store_transfer_bin_to_bin.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_NLPLA) TYPE  LTAP_NLPLA
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_LGORT) TYPE  LGORT_D DEFAULT '0002'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T
*"----------------------------------------------------------------------
  DATA: lt_data  TYPE ty_t_data,
        lr_data  TYPE REF TO zwm_store_stru,
        lr_data2 TYPE REF TO ty_data.

  DATA: l_matnr TYPE matnr,
        l_exidv TYPE exidv,
        l_tabix TYPE sytabix,
        l_rc    TYPE sysubrc.

  DATA: lt_hu  TYPE hum_hu_item_t,
        lt_hdr TYPE hum_hu_header_t.

  DATA: lr_hu     TYPE REF TO vepovb.

  DATA: lt_ltap_create TYPE STANDARD TABLE OF ltap_creat      INITIAL SIZE 0,
        lt_ltap        TYPE STANDARD TABLE OF ltap_vb         INITIAL SIZE 0,
        lt_vekp        TYPE STANDARD TABLE OF vekp            INITIAL SIZE 0.

  DATA: ls_ltap_create TYPE ltap_creat,
        ls_vekp        TYPE vekp,
        l_tanum        TYPE tanum.

  DATA: ls_husdc TYPE zmm_husdc,
        lt_husdc TYPE STANDARD TABLE OF zmm_husdc INITIAL SIZE 0.

  DATA: lv_count TYPE mblpo.

  DATA: l_vgbel  TYPE vgbel.

  DATA: l_mblnr  TYPE mblnr,
        l_mjahr  TYPE mjahr,
        l_tanum2 TYPE tanum,
        l_user   TYPE wwwobjid.

  DATA: lt_stock TYPE zwm_store_stru_t.

  TRANSLATE im_nlpla TO UPPER CASE.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_user
    IMPORTING
      output = l_user.

  LOOP AT it_data REFERENCE INTO lr_data.

    TRANSLATE lr_data->bin TO UPPER CASE.
* Convert into internal format
*    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT' "VKS-11.12.2021
*      EXPORTING
*        input  = lr_data->material
*      IMPORTING
*        output = l_matnr.
    l_matnr = lr_data->material.

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

  IF it_data[] IS NOT INITIAL.
    SELECT lgnum AS wm_no
           matnr AS material
           werks AS plant
           charg AS batch
           lgtyp AS storage_type
           lgpla AS bin
           verme AS avl_stock
      FROM lqua
      INTO CORRESPONDING FIELDS OF TABLE lt_stock
      FOR ALL ENTRIES IN it_data
     WHERE lgnum EQ im_lgnum
       AND werks EQ im_werks
       AND lgtyp EQ im_werks+1(3)
       AND lgpla EQ it_data-bin.

    IF sy-subrc IS INITIAL.
      SORT lt_stock BY wm_no material plant bin.
    ENDIF.
  ENDIF.

  PERFORM f_trannsfer_bin_to_bin USING im_lgnum
                                       im_werks
                                       im_lgort
                                       im_nlpla
                                       lt_data
                                       lt_stock
                              CHANGING
                                       ex_tanum
                                       ex_return.

*  PERFORM f_transfer_stock USING im_lgnum
*                                 im_werks
*                                 '0001'
*                                 '0002'
*                                 lt_data
*                        CHANGING ex_mblnr
*                                 ex_mjahr
*                                 ex_tanum
*                                 ex_return.

ENDFUNCTION.