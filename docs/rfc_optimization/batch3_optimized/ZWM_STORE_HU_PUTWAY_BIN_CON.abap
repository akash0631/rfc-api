function zwm_store_hu_putway_bin_con.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------
BREAK-POINT id Z_V2CHECK.
  data : lt_data type zwm_store_stru_t .

  data : lv_werks type werks_d.

  field-symbols : <lfs_data> type zwm_store_stru.

  if im_werks is initial .
    ex_return-type = c_error.
    ex_return-message = 'Blank Site Not allowed'.
    return.
  else.
    lv_werks = im_werks .
  endif.

  if im_werks is initial .
    ex_return-type = c_error.
    ex_return-message = 'Blank Site Not allowed'.
    return.
  endif.

  if it_data[] is  initial.
    ex_return-type = c_error.
    ex_return-message = 'No Data For process'.
    return.
  endif.

  lt_data[] = it_data[].
  loop at lt_data assigning <lfs_data>.

    translate <lfs_data>-bin to upper case .

    call function 'CONVERSION_EXIT_ALPHA_INPUT'
      exporting
        input  = <lfs_data>-hu_no
      importing
        output = <lfs_data>-hu_no.
  endloop.

  perform f_crate_to_from_bin_cons using lt_data lv_werks CHANGING ex_return.

endfunction.