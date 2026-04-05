FUNCTION ZWM_STORE_GET_001_V11_REV_POST.
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

 data:
      lt_data    type ty_t_data,
      lt_hst     type ty_t_hst,
      lr_data    type ref to zwm_store_stru,
      lr_data2   type ref to ty_data.

  data:
      l_matnr    type matnr,
      l_exidv    type exidv,
      l_tabix    type sytabix,
      l_number   type tanum,
      l_doc_type type char1,
      l_rc       type sysubrc.

  data:
     lt_ltap_create type standard table of ltap_creat      initial size 0,
     lt_ltap        type standard table of ltap_vb         initial size 0,
     lt_vekp        type standard table of vekp            initial size 0.

  data:
     ls_ltap_create type ltap_creat,
     ls_st_active   type zwm_st_active,
     ls_vekp        type vekp,
     ls_data        type zwm_store_stru ,
     ls_pick        type zst_pick ,
     l_tanum        type tanum.


data:
      lv_count type mblpo.

  data:
      l_vgbel  type vgbel,
      lv_lgort type lgort_d.

  data:
     l_mblnr  type mblnr,
     l_mjahr  type mjahr,
     l_tanum2 type tanum,
     l_user   type wwwobjid.


 call function 'CONVERSION_EXIT_ALPHA_INPUT'
    exporting
      input  = im_user
    importing
      output = l_user.

  translate l_user to upper case .
BREAK-POINT id Z_V2CHECK.
   loop at it_data reference into lr_data.

      translate lr_data->bin to upper case .

* Convert into internal format
      call function 'CONVERSION_EXIT_ALPHA_INPUT'
        exporting
          input  = lr_data->material
        importing
          output = lr_data->material.

        append initial line to lt_data reference into lr_data2.
        move:
          lr_data->material  to lr_data2->matnr,
          im_werks+1(3)  to  lr_data2->lgtyp,
          lr_data->bin to lr_data2->lgpla,
          lr_data->scan_qty to lr_data2->menge.

    endloop.

lv_lgort = '0001'.
   l_doc_type = 'O'.
    perform f_save_temp_data_putway_1 using
                                           lt_data
                                           im_werks
                                           l_user
                                           '0001'
                                           lv_lgort
                                           l_number
                                           l_doc_type
                                           lt_hst.

    perform f_putway_stock_1 using   lt_hst
                          changing ex_tanum
                                   ex_return.





ENDFUNCTION.