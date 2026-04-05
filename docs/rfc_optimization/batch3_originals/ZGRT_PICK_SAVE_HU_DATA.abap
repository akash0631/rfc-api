function zgrt_pick_save_hu_data.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA STRUCTURE  ZGRT_ORDCONF OPTIONAL
*"----------------------------------------------------------------------
  data : ls_return type bapiret2.
  data : lt_ordconf type table of zgrt_ordconf.
  data: lv_posnr type posnr.
  if im_user is initial.
    ls_return-type = 'E'.
    ls_return-message = 'User ID Cannot Be Blank'.
    ex_return = ls_return.
    return.
  endif.

  if it_data[] is initial.
    ls_return-type = 'E'.
    ls_return-message = 'No Data To Save'.
    ex_return = ls_return.
    return.
  else.
    select * from mara
      for all entries in @it_data
      where matnr = @it_data-matnr
      into table @data(lt_mara).
    sort lt_mara by matnr.


    loop at it_data assigning field-symbol(<lfs_data>).

      call function 'CONVERSION_EXIT_ALPHA_INPUT'
        exporting
          input  = <lfs_data>-exidv
        importing
          output = <lfs_data>-exidv.
    endloop.

    select * from zwm_exref
      for all entries in @it_data
      where exidv = @it_data-exidv
      and   sap_hu is initial
      into table @data(lt_exref).

    select * from zwm_wave_zone
            into table @data(lt_wave)
              for all entries in @lt_exref
                  where dwerks = @lt_exref-dwerks
                    and mapped = 'X'
                    and ex_hu = ''.
    sort lt_wave by matnr .
    sort lt_exref by exidv.
  endif.
  loop at it_data into data(ls_data).

    lv_posnr += 1.
    ls_data-posnr = conv posnr( lv_posnr ).
    read table lt_mara transporting no fields with key matnr = conv matnr18( ls_data-matnr ) binary search.
    if sy-subrc ne 0.
      ls_return-type = 'E'.
      concatenate 'Error In Save!! Invalid Material :' ls_data-matnr into ls_return-message separated by space.
      ex_return = ls_return.
      return.
    endif.
    if ls_data-exidv is initial.
      ls_return-type = 'E'.
      ls_return-message = 'Error In Save!! External HU Cannot Be Blank'.
      ex_return = ls_return.
      return.
    else.
      ls_data-exidv =  conv exidv( ls_data-exidv ).
      read table lt_exref into data(ls_exref) with key exidv = ls_data-exidv binary search.
      if sy-subrc ne 0.
        ls_return-type = 'E'.
        ls_return-message = 'Error In Save!! External HU Is Invalid'.
        ex_return = ls_return.
        return.
      else.
        if ls_data-werks is initial.
          ls_return-type = 'E'.
          ls_return-message = 'Error In Save!! Destination Store Cannot Be Blank'.
          ex_return = ls_return.
          return.
        elseif ls_data-werks ne ls_exref-dwerks.
          ls_return-type = 'E'.
          ls_return-message = 'Error In Save!! Destination Store Is Invalid'.
          ex_return = ls_return.
          return.
        endif.
      endif.
    endif.

    read table lt_wave assigning field-symbol(<lfs_wave>) with key matnr = ls_data-matnr
                                                                   ex_hu = ''.
    if sy-subrc is initial .
      <lfs_wave>-ex_hu =  ls_data-exidv.
      <lfs_wave>-ex_hu_date =  sy-datum.
      <lfs_wave>-ex_hu_time =  sy-uzeit.
      <lfs_wave>-ex_hu_user =  im_user.
    endif.

    ls_data-erdat = sy-datum.
    ls_data-ernam = sy-uname.
    ls_data-erzet = sy-uzeit.
    ls_data-alloc_date = <lfs_wave>-alloc_date.
    ls_data-alloc_number = <lfs_wave>-alloc_number.
    ls_data-swerks = <lfs_wave>-swerks.
    append ls_data to lt_ordconf.
    clear ls_data.
  endloop.

  if lt_ordconf[] is not initial.
    update zwm_wave_zone from table lt_wave.
    modify zgrt_ordconf from table lt_ordconf.
    commit work.
    ex_return-type = 'S'.
    ex_return-message = 'Saved Successfully'.
  endif.



endfunction.