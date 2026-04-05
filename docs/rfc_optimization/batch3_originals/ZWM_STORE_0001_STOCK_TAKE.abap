FUNCTION zwm_store_0001_stock_take.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_STOCK_TAKE) TYPE  XFLD OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"     VALUE(EX_MBLNR) TYPE  MBLNR
*"     VALUE(EX_MJAHR) TYPE  MJAHR
*"  TABLES
*"      IT_DATA STRUCTURE  VEPOVB OPTIONAL
*"----------------------------------------------------------------------
BREAK-POINT ID Z_V2CHECK.
  TYPES :  BEGIN OF ty_mara,
            matnr TYPE matnr ,
            attyp TYPE attyp,
          END OF ty_mara .

  DATA:
      lt_data    TYPE ty_t_data,
      lt_hst     TYPE ty_t_hst,
      lr_data    TYPE REF TO vepovb,
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
     lt_mara        TYPE STANDARD TABLE OF ty_mara         INITIAL SIZE 0,
     lt_vekp        TYPE STANDARD TABLE OF vekp            INITIAL SIZE 0.

  DATA:
     ls_ltap_create TYPE ltap_creat,
     ls_st_active   TYPE zwm_st_active,
     ls_vekp        TYPE vekp,
     l_tanum        TYPE tanum.

  DATA:
     ls_husdc TYPE zmm_husdc,
     ls_hst   TYPE ty_hst,
     lt_husdc TYPE STANDARD TABLE OF zmm_husdc INITIAL SIZE 0.

  DATA:
      lv_count TYPE mblpo.

  DATA:
      l_vgbel  TYPE vgbel.

  DATA:
     l_mblnr  TYPE mblnr,
     l_mjahr  TYPE mjahr,
     l_tanum2 TYPE tanum,
     l_user   TYPE wwwobjid.

  DATA:
     l_lgort_src TYPE lgort_d,
     l_lgort_dst TYPE lgort_d.

  READ TABLE it_data WITH KEY  lgpla = ''
                             TRANSPORTING NO FIELDS .
  IF sy-subrc IS INITIAL .
    ex_return-type = c_error.
    ex_return-message = 'Blank Bin Not allowed'.
    RETURN.
  ENDIF.

  READ TABLE it_data INDEX 1 .
  IF sy-subrc IS NOT INITIAL .
    ex_return-type = c_error.
    ex_return-message = 'Blank Data Not allowed'.
    RETURN.
  ENDIF.


  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_user
    IMPORTING
      output = l_user.


  LOOP AT it_data REFERENCE INTO lr_data.

    TRANSLATE lr_data->lgpla TO UPPER CASE .
* Convert into internal format
    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        input  = lr_data->matnr
      IMPORTING
        output = l_matnr.

    READ TABLE lt_data REFERENCE INTO lr_data2
    WITH KEY matnr = l_matnr
             lgpla = lr_data->lgpla.

    IF sy-subrc IS NOT INITIAL.
      APPEND INITIAL LINE TO lt_data REFERENCE INTO lr_data2.
      MOVE:
        l_matnr        TO lr_data2->matnr,
        lr_data->lgpla TO lr_data2->lgpla.
    ENDIF.

    lr_data2->menge = lr_data2->menge + lr_data->vemng.

  ENDLOOP.


  IF im_stock_take IS INITIAL.

    l_doc_type = 'U'.

    SELECT   matnr attyp FROM mara
                          INTO TABLE lt_mara
                          FOR ALL ENTRIES IN lt_data
                            WHERE matnr = lt_data-matnr
                             AND  attyp = '11'.
    IF sy-subrc IS INITIAL .
      ex_return-type = c_error.
      ex_return-message = 'PPK article not allow'.
      RETURN.
    ENDIF.

  ELSE.

*    l_lgort_src = '0003'.
    l_lgort_src = '0007'.
    l_doc_type = 'U'.
  ENDIF.

  l_lgort_dst = '0001'.

*  PERFORM f_save_temp_data_st_take USING
  PERFORM f_save_temp_data_st_take_001 USING
                                 lt_data
                                 l_lgort_src
                                 l_lgort_dst
                                 l_number
                                 im_werks
                                 l_user
                                 l_doc_type
                                 lt_hst.

  PERFORM f_putway_stock_001_stk USING   lt_hst
                               CHANGING ex_tanum
                                     ex_return..
*  PERFORM f_transfer_stock_0001 USING im_lgnum
**  PERFORM f_transfer_stock USING im_lgnum
*                                 im_werks
*                                 l_lgort_src
*                                 l_lgort_dst
*                                 lt_data
*                                 l_number
*                                 l_doc_type
*                        CHANGING ex_mblnr
*                                 ex_mjahr
*                                 ex_tanum
*                                 ex_return.

ENDFUNCTION.