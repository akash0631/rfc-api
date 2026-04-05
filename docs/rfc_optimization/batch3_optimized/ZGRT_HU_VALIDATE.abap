function zgrt_hu_validate.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_STORE) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_HU) TYPE  ZSAPHU OPTIONAL
*"     VALUE(IM_HU_CLOSED) TYPE  CHAR1 OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"----------------------------------------------------------------------

  data :ls_return type bapiret2,
        ls_hu_map type zgrt_ht_hu_map,
        ls_log    type zgrt_sin_log,
        lt_log    type table of zgrt_sin_log.
  if im_user is initial.
    ls_return-message = |User Id Cannot Be Blank.|.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  endif.
  if im_store is initial and im_hu_closed ne 'N'.
    ls_return-message = |Store Cannot Be Blank.|.
    ls_return-type = 'E'.
    ex_return = ls_return.
    return.
  endif.
  if im_hu is initial.
    ls_return-message = |HU Cannot Be Blank.|.
    ls_return-type = 'E'.
    ls_return-number = 01.
    ex_return = ls_return.
    return.
*  elseif im_hu+0(1) NE '2'.
*    ls_return-message = |Invalid HU.|.
*    ls_return-type = 'E'.
*    ls_return-number = 01.
*    ex_return = ls_return.
*    return.
  endif.

  im_hu = |{ im_hu alpha = in }|.

  if sy-subrc is not initial.
    ls_return-message = |HU is open| && | { im_hu } |.
    ls_return-type = 'E'.
    ls_return-number = 01.
    ex_return = ls_return.
    return.
  endif.

  select count( * ) as c
    from zgrt_ordconf
   where exidv eq @im_hu
     and hu_close eq 'N'
    into @data(l_count).

  if l_count is not initial and IM_HU_CLOSED ne 'N'.
    data(lv_hu1) = im_hu.
    shift lv_hu1 left deleting leading '0'.
    ls_return-message = |HU Already closed| && | { lv_hu1 } |.
    ls_return-type = 'E'.
    ls_return-number = 01.
    ex_return = ls_return.
    return.
  endif.


  if im_store is not initial.
    im_hu = |{ im_hu alpha = in }|.
**start bharat on 16.07.2023
*    select single dwerks,exidv,sap_hu into @data(ls_store)                                                      " --bharat on 16.07.2023
*                         from zwm_exref where dwerks eq @im_store and exidv eq @im_hu ."and sap_hu eq @space.   " --bharat on 16.07.2023

    select single dwerks,exidv,sap_hu into @data(ls_store)
                         from zwm_exref where exidv eq @im_hu ."and sap_hu eq @space.
** end bharat on 16.07.2023

    if sy-subrc is not initial .
      ls_return-message = |Invalid Details in zwm_exref|.
      ls_return-type = 'E'.
      ls_return-number = 01.
      ex_return = ls_return.
      return.
    endif.
**start bharat on 16.07.2023
    if ls_store-dwerks is not initial and ls_store-dwerks ne im_store .
      ls_return-message = |Invalid Details in zwm_exref with Store|.
      ls_return-type = 'E'.
      ls_return-number = 01.
      ex_return = ls_return.
      return.
    endif .
** end bharat on 16.07.2023

    if sy-subrc is initial and ls_store-exidv is not initial  and ls_store-sap_hu is not initial.
      ls_return-message = |SAP HU already Created|.
      ls_return-type = 'E'.
      ls_return-number = 01.
      ex_return = ls_return.
      return.
    endif.
**start bhart on 16.07.2023
*    if ls_store-dwerks is INITIAL and ls_store-exidv is NOT INITIAL.
*      ls_store-dwerks = im_store .
*      update zwm_exref set dwerks = ls_store-dwerks WHERE exidv = ls_store-exidv.
*       COMMIT WORK.
*      endif.
** end bharat on 16.07.2023
  endif.
*  if sy-subrc eq 0.
  select single * from zgrt_ht_hu_map into @data(ls_data) where werks eq @ls_store-dwerks ."AND ext_hu EQ @ls_store-exidv.

*  select single @abap_true from zgrt_ht_hu_map into @data(exists) where werks eq @ls_store-dwerks ."AND ext_hu EQ @ls_store-exidv.

  if sy-subrc is initial and im_hu_closed eq 'Y' and ls_data-ext_hu is not initial." AND im_hu_closed NE 'N').
    ls_return-message = |EX HU { ls_data-ext_hu } already mapped with store|.
    ls_return-type = 'S'.
    ex_return = ls_return.
    clear:ls_return, ls_hu_map .
    return .
  endif.

  if im_hu_closed eq 'Y'." AND im_hu_closed NE ''.
    data(lv_chck) = abap_true.
    ls_hu_map-werks = ls_store-dwerks.
    ls_hu_map-ext_hu = ls_store-exidv.
    modify zgrt_ht_hu_map from ls_hu_map .
    ls_return-message = |Store validated & Mapped|.
    ls_return-type = 'S'.
    ex_return = ls_return.
  endif.

  if im_hu_closed eq 'N'.
    im_hu = |{ im_hu alpha = in }|.
    select single * from zgrt_ht_hu_map into @data(ls_hu_d_map) where ext_hu eq @im_hu.
    if sy-subrc = 0.
      select * from zgrt_ordconf into table @data(lt_zgrt_ordconf) where exidv eq @ls_hu_d_map-ext_hu.
      if lt_zgrt_ordconf is not initial.
        loop at lt_zgrt_ordconf assigning field-symbol(<lfs_ordconf>).
          <lfs_ordconf>-hu_close = 'N'.
        endloop.
        modify zgrt_ordconf from table lt_zgrt_ordconf.
      endif.
      ls_hu_d_map-ext_hu = abap_false.
      modify zgrt_ht_hu_map from ls_hu_d_map.
      if ls_hu_d_map is not initial.
        ls_log-hu = im_hu.
        ls_log-store = ls_hu_d_map-werks.
        ls_log-log_date = sy-datum.
        ls_log-time = sy-uzeit.
        ls_log-user_id = im_user.
        modify zgrt_sin_log from ls_log.
      endif.
      commit work.
      data(lv_hu) = im_hu .
      shift lv_hu left deleting leading '0'.
      ls_return-message = |HU Closed| && | { lv_hu } |.
      ls_return-type = 'S'.
      ex_return = ls_return.
      return .
    endif.

    select single * from zgrt_sin into @data(ls_sin) where hu_tag eq @im_hu.
    if sy-subrc = 0.
      select * from zgrt_ordconf into table @data(lt_zgrt_ordconf_n) where exidv eq @ls_sin-hu_tag.
      if lt_zgrt_ordconf_n is not initial.
        loop at lt_zgrt_ordconf_n assigning field-symbol(<lfs_ordconf_n>).
          <lfs_ordconf_n>-hu_close = 'N'.
        endloop.
        modify zgrt_ordconf from table lt_zgrt_ordconf_n.
      endif.
      ls_sin-hu_tag = abap_false.
      modify zgrt_sin from ls_sin.
      if ls_sin is not initial.
        ls_log-hu = im_hu.
        ls_log-store = ls_sin-store.
        ls_log-zzone = ls_sin-zzone.
        ls_log-log_date = sy-datum.
        ls_log-time = sy-uzeit.
        ls_log-user_id = im_user.
        modify zgrt_sin_log from ls_log.
      endif.
      data(lv_hu2) = im_hu.
      shift lv_hu2 left deleting leading '0'.
      ls_return-message = |HU closed| && | { lv_hu2 } |.
      ls_return-type = 'S'.
      ex_return = ls_return.
      return .

    else.
      ls_return-message = |HU already closed|.
      ls_return-type = 'E'.
      ex_return = ls_return.
      return.
    endif.
  endif.
*      commit work.
*      ls_return-message = |HU Closed|.
*      ls_return-type = 'S'.
*      ex_return = ls_return.
*    endif.
  if im_hu_closed eq 'Y'.
*      select single * from zgrt_ordconf into @data(ls_zgrt_ordconf) where exidv eq @ls_store-exidv.
*      if sy-subrc = 0.
**          ls_zgrt_ordconf-hu_close = 'Y'.
**          MODIFY zgrt_ordconf FROM ls_zgrt_ordconf.
*      endif.
    select single * from zgrt_ht_hu_map into ls_hu_d_map where werks eq im_store.
    if ( ls_hu_d_map-ext_hu is initial and ls_hu_d_map-werks is not initial ) and im_hu_closed ne ''.
      ls_hu_d_map-ext_hu = im_hu.
      ls_return-message = |Store validated & Mapped|.
      ls_return-type = 'S'.
      ex_return = ls_return.
      modify zgrt_ht_hu_map from ls_hu_d_map.
      commit work and wait .
      return.
    else.
      ls_hu_d_map-ext_hu = im_hu.
      if lv_chck eq abap_false.
        ls_return-message = |Ext HU { im_hu }already mapped with store|.
        ls_return-type = 'E'.
        ex_return = ls_return.
        return.
      endif.
    endif.
  endif.
  clear:ls_return, ls_hu_map .",ls_zgrt_ordconf.
*  ENDIF.
*  else.
*    ls_return-message = |Invalid Details.|.
*    ls_return-type = 'E'.
*    ls_return-number = 02.
*    ex_return = ls_return.
*  endif.
  if im_hu_closed eq ''.
    select single @abap_true from zgrt_ht_hu_map into @data(exists_hu) where werks eq @im_store." AND ext_hu EQ @ls_store-exidv.
    if exists_hu eq abap_true and im_hu_closed eq ''." AND im_hu_closed NE 'N').
      ls_return-message = |External HU already mapped with store|.
      ls_return-type = 'E'.
      ex_return = ls_return.
      clear:ls_return, ls_hu_map .
    endif.
  endif.
  if ex_return-message is initial.
    ls_return-message = |Invalid Details.|.
    ls_return-type = 'E'.
    ls_return-number = 02.
    ex_return = ls_return.
  endif.
endfunction.