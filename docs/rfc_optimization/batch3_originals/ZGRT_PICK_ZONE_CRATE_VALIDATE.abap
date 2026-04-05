function zgrt_pick_zone_crate_validate.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_CRATE) TYPE  ZZCRATE OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_DATA STRUCTURE  ZWM_WAVE_ZONE OPTIONAL
*"      ET_EAN_DATA STRUCTURE  MEAN OPTIONAL
*"----------------------------------------------------------------------
  data : ls_return type bapiret2,
         ls_crate  type zwm_crate.
  if im_user is initial.
    ls_return-message = 'User Id Cannot Be Blank.'.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  endif.
*****Add by Kunal 01.02.23 As this process is closed now new process COMBO need to follow
  if im_crate is not initial.
    ls_return-message = | This process is closed now |.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  endif.
***********END Of Kunal 01.02.23********************************
  if im_crate is initial.
    ls_return-message = 'Crate Cannot Be Blank.'.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  else.

*    if im_crate+0(8) ne 'GRT-ZONE'.
*      ls_return-MESSAGE = 'Scan GRT ZONE only'.
*      ls_return-TYPE = 'E'.
*      ex_return = ls_return.
*      RETURN.
  endif.

  select single *
  from zwm_crate
  into ls_crate
  where crate = im_crate
  and  lgnum = 'V2R'.
*    AND  msa_empty = ''.

  if sy-subrc is not initial.
    ls_return-message = 'Invalid Crate Or Is Not Tagged To A Bin.'.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  else.
    ex_return-type = 'S'.
    ex_return-message = 'Valid'.

    select * from zwm_wave_zone
      into table @et_data
      where crate = @im_crate
      and picked = 'X'
      and zmapped = ''
      and mapped = ''
      and complete = ''
      and etype = 'GRT'.
    if sy-subrc eq 0.
      select * from mean
        into table @et_ean_data
        for all entries in @et_data
      where matnr = @et_data-matnr.
    else.
      ls_return-message = 'MSA Crate is already picked'.
      ls_return-type = 'E'.
      ex_return = ls_return.
      return.
    endif.
  endif.
*endif.






endfunction.