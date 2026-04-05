FUNCTION ZWM_STORE_DIRECT_PICKING_PPL.
*"--------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"     VALUE(EX_MBLNR) TYPE  MBLNR
*"     VALUE(EX_MJAHR) TYPE  MJAHR
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"--------------------------------------------------------------------
  DATA: lt_data  TYPE ty_t_data,
        lr_data  TYPE REF TO zwm_store_stru,
        lr_data2 TYPE REF TO ty_data.

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
        ls_data        TYPE zwm_store_stru,
        ls_pick        TYPE zst_pick,
        l_tanum        TYPE tanum.

  DATA: ls_husdc TYPE zmm_husdc,
        lt_husdc TYPE STANDARD TABLE OF zmm_husdc INITIAL SIZE 0.

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
  READ TABLE it_data INTO ls_data INDEX 1.

  IF ls_data-picnr IS INITIAL.
    SELECT SINGLE *
      FROM zwm_st_active
      INTO ls_st_active
      WHERE lgnum = im_lgnum
        AND werks = im_werks.

    IF sy-subrc IS INITIAL.
      IF ls_st_active-picking IS INITIAL.
        ex_return-type = c_error.
        CONCATENATE 'Direct Picking for site' im_werks 'not allowed !!' INTO ex_return-message SEPARATED BY space.
        RETURN.
      ENDIF.
    ELSE.
      ex_return-type = c_error.
      CONCATENATE 'entry missing in ZSDC_ST_ALLOW for ' im_werks INTO ex_return-message SEPARATED BY space.
      RETURN.
    ENDIF.
  ENDIF.

  IF ex_return-message IS INITIAL.
    LOOP AT it_data REFERENCE INTO lr_data.
      TRANSLATE lr_data->bin TO UPPER CASE.

      l_matnr = lr_data->material.
      READ TABLE lt_data REFERENCE INTO lr_data2 WITH KEY matnr = l_matnr lgpla = lr_data->bin.
      IF sy-subrc IS NOT INITIAL.
        APPEND INITIAL LINE TO lt_data REFERENCE INTO lr_data2.

        CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
          EXPORTING
            input  = lr_data->picnr
          IMPORTING
            output = lr_data->picnr.

        MOVE: l_matnr        TO lr_data2->matnr,
              im_werks+1(3)  TO  lr_data2->lgtyp,
              lr_data->bin   TO lr_data2->lgpla,
              lr_data->picnr TO lr_data2->picnr.
      ENDIF.
      lr_data2->menge = lr_data2->menge + lr_data->scan_qty.
    ENDLOOP.

*Set location
    DATA: ls_store TYPE zwms_store_0008.

    SELECT SINGLE *
      FROM zwms_store_0008
      INTO ls_store
      WHERE werks = im_werks.

    IF ls_store-active IS INITIAL.
      lv_lgort = '0001'.
    ELSE.
      lv_lgort = '0008'.
    ENDIF.

    l_doc_type = '3'.

    IF im_werks = 'HD22'. "VKS-19.03.2021
      lv_lgort = '0008'.
    ENDIF.

    PERFORM f_save_temp_data_picking USING lt_data
                                           im_werks
                                           l_user
                                           '0002'
                                           lv_lgort
                                           l_number
                                           l_doc_type.

    PERFORM f_transfer_stock USING im_lgnum
                                   im_werks
                                   '0002'
                                   lv_lgort
                                   lt_data
                                   l_number
                                   l_doc_type
                          CHANGING ex_mblnr
                                   ex_mjahr
                                   ex_tanum
                                   ex_return.

    PERFORM f_clear_v04_from_msa_bin USING  im_lgnum
                                            im_werks
                                            '0002'
                                            lv_lgort
                                            lt_data
                                            l_number
                                            l_doc_type
                                   CHANGING ex_mblnr
                                            ex_mjahr
                                            ex_tanum
                                            ex_return.
  ENDIF.

*Movement 0008-0001 for DH24 "VKS-19.03.2021
  IF im_werks = 'HD22'.
    DATA: ls_mean     TYPE mean,
          ex_mard     TYPE mard,
          et_ean_data TYPE TABLE OF marm.

    LOOP AT it_data INTO ls_data.
      SELECT SINGLE * FROM mean INTO ls_mean WHERE matnr = ls_data-material AND hpean = 'X'.

      CALL FUNCTION 'ZWM_STORE_GET_STOCK'
        EXPORTING
          im_werks      = im_werks
          im_lgort      = '0001'
          im_ean11      = ls_mean-ean11
          im_stock_take = ''
        IMPORTING
          ex_return     = ex_return
          ex_mard       = ex_mard
        TABLES
          et_ean_data   = et_ean_data.

      REFRESH: et_ean_data.
      CLEAR: ls_data,ls_mean,ex_mard.
    ENDLOOP.

    CALL FUNCTION 'ZWM_STORE_TRANSFER_SLOC_TO_SLO'
      EXPORTING
        im_werks      = im_werks
        im_lgort_src  = '0008'
        im_lgort_dest = '0001'
        im_user       = im_user
        im_lgnum      = 'SDC'
      IMPORTING
        ex_return     = ex_return
        ex_tanum      = ex_tanum
        ex_mblnr      = ex_mblnr
        ex_mjahr      = ex_mjahr
      TABLES
        it_data       = it_data.
  ENDIF.

ENDFUNCTION.