function zgrt_zone_crate_sin_validate.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_CRATE) TYPE  ZZCRATE OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_ZONE) TYPE  ZONE OPTIONAL
*"  EXPORTING
*"     VALUE(EX_MATNR) TYPE  MATNR
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_DATA TYPE  ZGRT_ZONE_DATA_TT OPTIONAL
*"      ET_EAN_DATA STRUCTURE  MEAN OPTIONAL
*"----------------------------------------------------------------------
  data : ls_return type bapiret2,
         ls_crate  type zwm_crate.
  data ls_grt_ordconf type zgrt_ordconf.
  data lv_posnr type posnr value '000010'.
  data ls_data type zgrt_zone_data.
  data : lv_menge type menge_d.

*BBLU-03301
* GRT-ZONE2

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
  if im_crate(01) = 'Z'.
    ls_return-message = 'Zone crate is not allowed'.
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
    ls_return-message = 'Invalid Crate Or Is Not Tagged To Bin.'.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  endif.
  if line_exists( et_data[ confirmed = abap_true ] ).
    data(lv_flag) = abap_true.
    sort et_data by dwerks.
    delete adjacent duplicates from et_data comparing dwerks.
    delete et_data where confirmed eq ''.
    select * from zwm_wave_zone into table @data(lt_wave)
                                for all entries in @et_data
                                where dwerks eq @et_data-dwerks
                                and crate = @im_crate" AND zone_crate
                                and etype eq 'SIN'
*                                and csortmapped eq @abap_true
                                and mapped eq ''. " Added by amit
    if sy-subrc <> 0.
      ls_return-message = 'No data found'.
      ls_return-type = 'E'.
      ex_return = ls_return.
      return.
    endif.

    data(lv_werks) = value #( lt_wave[ 1 ]-dwerks optional ).
    if lv_werks is not initial.
      read table et_data assigning field-symbol(<lfs_data>) with key dwerks = lv_werks confirmed = abap_true.
      if sy-subrc = 0.
        select single * from zgrt_sin into @data(ls_sin) where store eq @lv_werks and zzone = @<lfs_data>-zone .
        if ls_sin-hu_tag is initial.
          ls_return-message = 'Please Map HU with' && | { lv_werks } | && | { <lfs_data>-zone } |.
          ls_return-type = 'E'.
          ex_return = ls_return.
          return.
        endif.
        select single hu_close  from zgrt_ordconf as a
                    where exidv = @ls_sin-hu_tag and hu_close eq 'N'
                    into @data(lv_close).
        if lv_close = 'N'.
          ls_return-message = 'HU Allready Close' && | { ls_sin-hu_tag } |.
          ls_return-type = 'E'.
          ex_return = ls_return.
          return.
        endif.

        select * from zgrt_ordconf into table @data(lt_grt_conf) where exidv = @ls_sin-hu_tag and hu_close ne 'N'.
        if sy-subrc = 0.
          sort lt_grt_conf ascending by posnr.  " ++bharat on 17.12.2022
          data(lv_line) = lines( lt_grt_conf ).
          data(ls_hu_exist) = value #( lt_grt_conf[ lv_line ] optional ).
        endif.
*        do <lfs_data>-e_menge times.
        clear lv_menge .
        loop at lt_wave assigning field-symbol(<lfs_wave>) where dwerks eq <lfs_data>-dwerks.." AND csortmapped EQ abap_false
          <lfs_wave>-ex_hu = ls_sin-hu_tag.
          <lfs_wave>-ex_hu_date = sy-datum.
          <lfs_wave>-ex_hu_time = sy-uzeit.
          <lfs_wave>-ex_hu_user = im_user.
          <lfs_wave>-zone_crate = im_zone.
          <lfs_wave>-mapped = abap_true.
          <lfs_wave>-mapped_date = sy-datum.
          <lfs_wave>-mapped_time = sy-uzeit.
          <lfs_wave>-mapped_user = im_user.
          if ls_hu_exist-posnr is not initial.
            ls_hu_exist-posnr = ls_hu_exist-posnr + 1.
*        ls_grt_ordconf = corresponding #( ls_wave ).
            ls_grt_ordconf-exidv = ls_sin-hu_tag.
            ls_grt_ordconf-matnr = <lfs_wave>-matnr.
            ls_grt_ordconf-swerks = <lfs_wave>-swerks.
            ls_grt_ordconf-werks = ls_sin-store.
            ls_grt_ordconf-menge = 1.
            ls_grt_ordconf-ebeln = abap_false.
            ls_grt_ordconf-vbeln = abap_false.
            ls_grt_ordconf-tanum = abap_false.
            ls_grt_ordconf-erdat = sy-datum.
            ls_grt_ordconf-ernam = sy-uname. "++ kunal
            ls_grt_ordconf-erzet = sy-uzeit."++ kunal
            ls_grt_ordconf-lgnum = 'V2R'.
*            lv_hu_posnr = lv_hu_posnr + 1.
            ls_grt_ordconf-posnr = ls_hu_exist-posnr .
          else.
*        ls_grt_ordconf = corresponding #(  ls_wave ).
            ls_grt_ordconf-exidv = ls_sin-hu_tag.
            ls_grt_ordconf-matnr = <lfs_wave>-matnr.
            ls_grt_ordconf-swerks = <lfs_wave>-swerks.
            ls_grt_ordconf-menge = 1.
            ls_grt_ordconf-werks = ls_sin-store.
            ls_grt_ordconf-erdat = sy-datum.
            ls_grt_ordconf-ernam = sy-uname. "++ kunal
            ls_grt_ordconf-erzet = sy-uzeit."++ kunal
            ls_grt_ordconf-ebeln = abap_false.
            ls_grt_ordconf-tanum = abap_false.
            ls_grt_ordconf-lgnum = 'V2R'.
            ls_grt_ordconf-posnr = lv_posnr.
          endif.
          modify zgrt_ordconf from ls_grt_ordconf.
          lv_posnr = lv_posnr + 1.
          lv_menge = lv_menge + 1 .
          if lv_menge = <lfs_data>-e_menge .
            exit.
          endif.
        endloop.
*        enddo.
      endif.
      modify zwm_wave_zone from table lt_wave.
      commit work.
      ls_return-message = | ZONE Qty. has been confirmed successfully | .
      ls_return-type = 'S'.
      ex_return = ls_return.
      clear:
            lv_werks,
            lv_flag.
*      return.
    endif.
  endif.

  if lv_flag eq abap_false.
    refresh et_data[].
    select sum( menge ) as qty ,dwerks as plant, matnr as material
      into table @data(lt_wave_n) from zwm_wave_zone
                                  where crate = @im_crate
                                  and picked = 'X'
*                                and zmapped = 'X'
                                  and mapped = ''
                                  and complete = ''
                                  and ex_hu = ''
                                  and etype = 'SIN'
*                                and csortmapped eq 'X'
                                  group by dwerks, matnr.

    if lt_wave_n is not initial.
      sort lt_wave_n by plant.
      delete adjacent duplicates from lt_wave_n comparing plant.
      ex_matnr = lt_wave_n[ 1 ]-material.
    endif.
*    break-point.
    select
      a~store as werks,
*  a~ext_hu  from zgrt_ht_hu_map as a
  a~hu_tag as ext_hu  from zgrt_sin as a
      inner join @lt_wave_n as b
      on a~store = b~plant
      and zzone = @im_zone
      into table @data(lt_hu).



    loop at lt_wave_n assigning field-symbol(<lfs_zone>).
      ls_data-dwerks = <lfs_zone>-plant.
      ls_data-menge = <lfs_zone>-qty.
      ls_data-ex_hu = value #( lt_hu[ werks = <lfs_zone>-plant ]-ext_hu optional ).
      append ls_data to et_data.
      clear ls_data.
    endloop.



*    endif.
*    endif.

  endif.

endfunction.