FUNCTION ZWM_STORE_TRF_0001_TO_0010.
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
BREAK-POINT ID Z_V2CHECK.
  DATA:
      lt_data    TYPE ty_t_data,
      lr_data    TYPE REF TO zwm_store_stru,
      lr_data2   TYPE REF TO ty_data.

  DATA:
      l_matnr    TYPE matnr,
      l_exidv    TYPE exidv,
      l_tabix    TYPE sytabix,
      l_number   TYPE tanum,
      l_doc_type TYPE char1,
      l_rc       TYPE sysubrc.

  DATA:
     lt_ltap_create TYPE STANDARD TABLE OF ltap_creat      INITIAL SIZE 0,
     lt_ltap        TYPE STANDARD TABLE OF ltap_vb         INITIAL SIZE 0.

  DATA:
     ls_ltap_create TYPE ltap_creat,
     ls_st_active   TYPE zwm_st_active,
     ls_vekp        TYPE vekp,
     ls_data        TYPE zwm_store_stru ,
     ls_pick        TYPE zst_pick ,
     l_tanum        TYPE tanum.

  DATA:
     ls_husdc TYPE zmm_husdc,
     lt_husdc TYPE STANDARD TABLE OF zmm_husdc INITIAL SIZE 0.

  DATA:
      lv_count TYPE mblpo.

  DATA:
      l_vgbel  TYPE vgbel,
      lv_lgort TYPE lgort_d.

  DATA:
     l_mblnr  TYPE mblnr,
     l_mjahr  TYPE mjahr,
     l_tanum2 TYPE tanum,
     l_user   TYPE wwwobjid.


  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_user
    IMPORTING
      output = l_user.

  TRANSLATE l_user TO UPPER CASE .


  IF ex_return-message IS INITIAL.

    LOOP AT it_data REFERENCE INTO lr_data.

      TRANSLATE lr_data->bin TO UPPER CASE .

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

        CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
          EXPORTING
            input  = lr_data->picnr
          IMPORTING
            output = lr_data->picnr.


        MOVE:
          l_matnr        TO lr_data2->matnr,
          im_werks+1(3)  TO  lr_data2->lgtyp,
          lr_data->bin TO lr_data2->lgpla,
          lr_data->picnr TO lr_data2->picnr.
      ENDIF.

      lr_data2->menge = lr_data2->menge + lr_data->scan_qty.

    ENDLOOP.

    l_doc_type = 'V'.
*    PERFORM f_save_temp_data_picking USING
    PERFORM f_save_temp_data_0001_to_0010 USING
                                           lt_data
                                           im_werks
                                           l_user
                                           '0001'
                                           '0010'
                                           l_number
                                           l_doc_type.

    PERFORM f_transfer_stock_0001_to_0010 uSING im_lgnum
                                   im_werks
                                   '0001'
                                   '0010'
                                   lt_data
                                   l_number
                                   l_doc_type
                          CHANGING ex_mblnr
                                   ex_mjahr
                                   ex_tanum
                                   ex_return.

  perform f_clear_v04_from_msa_bin using  im_lgnum
                                   im_werks
                                   '0001'
                                   '0010'
                                   lt_data
                                   l_number
                                   l_doc_type
                          changing ex_mblnr
                                   ex_mjahr
                                   ex_tanum
                                   ex_return.
  ENDIF.

ENDFUNCTION.