FUNCTION zwm_store_get_v01_001s_post.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------
  DATA: lt_data    TYPE ty_t_data,
        lt_hst     TYPE ty_t_hst,
        lr_data    TYPE REF TO zwm_store_stru,
        lr_data2   TYPE REF TO ty_data.

  DATA: l_matnr    TYPE matnr,
        l_exidv    TYPE exidv,
        l_tabix    TYPE sytabix,
        l_number   TYPE tanum,
        l_doc_type TYPE char1,
        l_rc       TYPE sysubrc.

  DATA: lt_ltap_create TYPE STANDARD TABLE OF ltap_creat INITIAL SIZE 0,
        lt_ltap        TYPE STANDARD TABLE OF ltap_vb    INITIAL SIZE 0,
        lt_vekp        TYPE STANDARD TABLE OF vekp       INITIAL SIZE 0.

  DATA: ls_ltap_create TYPE ltap_creat,
        ls_st_active   TYPE zwm_st_active,
        ls_vekp        TYPE vekp,
        ls_data        TYPE zwm_store_stru ,
        ls_pick        TYPE zst_pick,
        l_tanum        TYPE tanum.

  DATA: lv_count TYPE mblpo,
        l_vgbel  TYPE vgbel,
        lv_lgort TYPE lgort_d,
        l_mblnr  TYPE mblnr,
        l_mjahr  TYPE mjahr,
        l_tanum2 TYPE tanum,
        l_user   TYPE wwwobjid.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_user
    IMPORTING
      output = l_user.

  TRANSLATE l_user TO UPPER CASE.
BREAK-POINT id Z_V2CHECK.
  LOOP AT it_data REFERENCE INTO lr_data.
    TRANSLATE lr_data->bin TO UPPER CASE.

* Convert into internal format
    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        input  = lr_data->material
      IMPORTING
        output = lr_data->material.

    APPEND INITIAL LINE TO lt_data REFERENCE INTO lr_data2.
    MOVE: lr_data->material  TO lr_data2->matnr,
          '122'  TO  lr_data2->lgtyp, "im_werks+1(3) VKS-11.03.2021
          lr_data->bin TO lr_data2->lgpla,
          lr_data->scan_qty TO lr_data2->menge.
  ENDLOOP.

  lv_lgort = '0001'.
  l_doc_type = 'O'.
  PERFORM f_save_temp_data_putway USING lt_data
                                        im_werks
                                        l_user
                                        '0001'
                                        lv_lgort
                                        l_number
                                        l_doc_type
                                        lt_hst.

  PERFORM f_putway_stock USING   lt_hst
                        CHANGING ex_tanum
                                 ex_return.
ENDFUNCTION.