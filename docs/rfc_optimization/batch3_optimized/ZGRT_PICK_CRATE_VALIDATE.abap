function zgrt_pick_crate_validate.
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

  if im_crate is initial.
    ls_return-message = 'Crate Cannot Be Blank.'.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  endif.

  if im_crate+0(8) ne 'GRT-ZONE'.
    ls_return-message = 'Only GRT ZONE bin allowed'.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
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
  endif.

*  select * from zwm_wave_zone
*      into table @et_data
*      where zone_crate = @im_crate
*      and csortmapped = 'X'
**      and zmapped = 'X'
**      and mapped = ''
**      and complete = ''
**      and ex_hu = ''
*      and etype = 'GRT'.
*    IF sy-subrc <> 0.
*      ls_return-message = 'Please first do the Sorting' .
*    ls_return-type = 'E'.
*    ex_return = ls_return.
*    return.
*    ENDIF.
  ex_return-type = 'S'.
  ex_return-message = 'Valid'.

*    SELECT * FROM zwm_wave_zone
*      INTO TABLE @et_data
*      WHERE zone_crate = @im_crate
*      AND picked = 'X'
*      AND zmapped = 'X'
*      AND mapped = ''
*      AND complete = ''
*      and ex_hu = ''
*      AND etype = 'GRT'.
**      and erdat gt '20220205'.
*    IF sy-subrc EQ 0.
*      SELECT * FROM mean
*        INTO TABLE @et_ean_data
*        FOR ALL ENTRIES IN @et_data
*      WHERE matnr = @et_data-matnr.
*      else.
*      ls_return-message = 'No Data found for '.
*      ls_return-type = 'E'.
*      ex_return = ls_return.
*      RETURN.
*    ENDIF.








endfunction.