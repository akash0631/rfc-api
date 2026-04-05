function zgrt_pick_save_pick_data.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_TANUM) TYPE  TANUM OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_TEST) TYPE  CHAR1 OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------

  field-symbols : <lfs_data> type zwm_store_stru.
  data : ls_data type zwm_store_stru.
  data : lt_ltap_create type standard table of ltap_creat.
  data : lv_tanumzone type tanum.
  data : lt_mara type tt_mara,
         ls_mara type ty_mara.
  data : ls_return   type bapiret2,
         ls_picklist type zgrt_picklist.
  data : ls_ltap_create type ltap_creat .
  data : lv_tanum   type tanum,
         lv_message type bapi_msg.
  data : lv_apino type zzapino.
  data : lv_posnr type posnr.
  data : lv_sammg type sammg.
  data : lt_data type zwm_store_stru_t.
  data : ls_crate type zwm_crate.
  clear lv_apino.
  data : lt_wavezone type standard table of zwm_wave_zone,
         ls_wavezone type zwm_wave_zone.
  field-symbols <lfs_zone> type zwm_wave_zone.
  ranges : r_wave for zwm_wave_zone-wave.
  data : lt_picklist type standard table of zgrt_picklist .

*  IF im_user IS INITIAL.
*    ls_return-type = 'E'.
*    ls_return-message = 'User ID Cannot Be Blank.'.
*    ex_return = ls_return.
*    RETURN.
*  ENDIF.

  if it_data[] is initial .
    ls_return-type = 'E'.
    ls_return-message = 'Plz check! No data for Post'.
    ex_return = ls_return.
    return.

  endif.

  if im_tanum is initial.
    ls_return-type = 'E'.
    ls_return-message = 'TO Number Cannot Be Blank.'.
    ex_return = ls_return.
    return.
  else.
    call function 'CONVERSION_EXIT_ALPHA_INPUT'
      exporting
        input  = im_tanum
      importing
        output = lv_tanumzone.

  endif.
  lt_data[] = it_data[].
  sort lt_data by plant crate.
  delete adjacent duplicates from lt_data comparing plant crate.

*  SELECT single * FROM zwm_wave_zone into @data(lv_swerks)
  select single * from zwm_wave_zone into @data(ls_wave)
 where tanum = @lv_tanumzone.

  data(lv_swerks) = ls_wave-swerks.
  data(lv_dwerks) = ls_wave-dwerks.

  if lv_swerks = 'DH24'.
    data(lv_lgtyp) = 'E01'.
  else.
    lv_lgtyp = 'E02'.
  endif.

  loop at lt_data assigning field-symbol(<lfs_data1>).

    <lfs_data1>-plant =  lv_swerks.
    translate <lfs_data1>-bin to upper case.
  endloop.

  refresh lt_data[].
  lt_data[] = it_data[].
  if lt_data[] is not initial.
    select * from zwm_wave_zone
    into table lt_wavezone
    for all entries in lt_data
    where tanum = lv_tanumzone
    and   lgpla = lt_data-bin
    and   picked = ''
    and etype = 'GRT'.
    if sy-subrc <> 0.
      select * from zwm_wave_zone
     into table lt_wavezone
     for all entries in lt_data
     where tanum = lv_tanumzone
     and   lgpla = lt_data-bin
     and   picked = ''
     and etype = 'SIN'.

    endif.
    select distinct 'I', 'EQ' , wave from @lt_wavezone as a into table @r_wave .
  endif.

  if lt_wavezone is initial.
    ls_return-type = 'E'.
    ls_return-message = 'No pending data for Post'.
    ex_return = ls_return.
    return.
  endif.
  select matnr meins
  from mara
  into table lt_mara
  for all entries in lt_wavezone
  where matnr = lt_wavezone-matnr.
  sort lt_mara by matnr.



  refresh it_data.

  loop at lt_wavezone into ls_wavezone.
    ls_data-material = ls_wavezone-matnr.
    ls_data-plant = lv_swerks.
    read table lt_mara into ls_mara with key matnr = ls_wavezone-matnr binary search.
    if sy-subrc eq 0.
      ls_data-meins = ls_mara-meins.
    endif.
    ls_data-stor_loc = '0001'.
    ls_data-bin = ls_wavezone-lgpla.
    ls_data-crate = ls_wavezone-crate.
*    SELECT SINGLE crate FROM zwm_crate
*    INTO ls_data-crate
*    WHERE lgpla = ls_data-bin
*    AND lgnum = 'V2R'.
    ls_data-sammg = ls_wavezone-wave.
    ls_data-scan_qty = ls_wavezone-menge.
    append ls_data to it_data.
    clear ls_mara.
    clear ls_data.
  endloop.
  sort lt_wavezone by tanum itemno vbeln_vl.

  check im_test is initial.

  loop at  it_data assigning <lfs_data>.
*    TRANSLATE <lfs_data>-material TO UPPER CASE.
*    TRANSLATE <lfs_data>-plant TO UPPER CASE.
*    TRANSLATE <lfs_data>-meins TO UPPER CASE.
    ls_ltap_create-matnr = <lfs_data>-material.
    ls_ltap_create-werks = <lfs_data>-plant.
    ls_ltap_create-lgort = <lfs_data>-stor_loc.
    ls_ltap_create-altme = <lfs_data>-meins.
    ls_ltap_create-anfme = <lfs_data>-scan_qty.
    ls_ltap_create-squit = 'X'.
    ls_ltap_create-vlber = '001'.                      "Source storage section
    ls_ltap_create-nlber = '001'.                      "Destination Storage Section
    ls_ltap_create-nltyp = 'V09'.                      "Destination Storage Type  " Chanegs by AMit For Combo picking 22 March 2023
    ls_ltap_create-nlpla = <lfs_data>-crate. "BBLU-03516"'BIN-GRT' ."           "Destination Storage Bin
    ls_ltap_create-vltyp = 'V06'.                      "Source Storage Type
    ls_ltap_create-vlpla = <lfs_data>-sammg.           "Source Bin
    ls_ltap_create-zeugn = lv_tanumzone.
    collect ls_ltap_create into lt_ltap_create.
    if lv_sammg is initial.
      lv_sammg = <lfs_data>-sammg.
    endif.
    clear ls_ltap_create.
  endloop.

  check lt_ltap_create[] is not initial .
  call function 'DEQUEUE_ALL'
    exporting
      _synchron = 'X'.

  call function 'L_TO_CREATE_MULTIPLE' destination 'NONE'
    exporting
      i_lgnum                = 'V2R'
      i_bwlvs                = '855' " GRT -V06-V09 Inv By Amit 27 St March 23
      i_kompl                = ''
      i_betyp                = 'G'
      i_benum                = lv_sammg "is_vbsk-sammg
    importing
      e_tanum                = lv_tanum
    tables
      t_ltap_creat           = lt_ltap_create
    exceptions
      no_to_created          = 1
      bwlvs_wrong            = 2
      betyp_wrong            = 3
      benum_missing          = 4
      betyp_missing          = 5
      foreign_lock           = 6
      vltyp_wrong            = 7
      vlpla_wrong            = 8
      vltyp_missing          = 9
      nltyp_wrong            = 10
      nlpla_wrong            = 11
      nltyp_missing          = 12
      rltyp_wrong            = 13
      rlpla_wrong            = 14
      rltyp_missing          = 15
      squit_forbidden        = 16
      manual_to_forbidden    = 17
      letyp_wrong            = 18
      vlpla_missing          = 19
      nlpla_missing          = 20
      sobkz_wrong            = 21
      sobkz_missing          = 22
      sonum_missing          = 23
      bestq_wrong            = 24
      lgber_wrong            = 25
      xfeld_wrong            = 26
      date_wrong             = 27
      drukz_wrong            = 28
      ldest_wrong            = 29
      update_without_commit  = 30
      no_authority           = 31
      material_not_found     = 32
      lenum_wrong            = 33
      matnr_missing          = 34
      werks_missing          = 35
      anfme_missing          = 36
      altme_missing          = 37
      lgort_wrong_or_missing = 38
      others                 = 39.
  if sy-subrc <> 0.

    call function 'FORMAT_MESSAGE'
      exporting
        id        = sy-msgid
        lang      = sy-langu
        no        = sy-msgno
        v1        = sy-msgv1
        v2        = sy-msgv2
        v3        = sy-msgv3
        v4        = sy-msgv4
      importing
        msg       = lv_message
      exceptions
        not_found = 1
        others    = 2.
    ls_return-type = 'E'.
    ls_return-message = lv_message.
    ex_return = ls_return.
    return.
  else.
    lv_message = ''.
    lv_posnr = 000000.
    call function 'NUMBER_GET_NEXT'
      exporting
        nr_range_nr = '06'
        object      = 'ZZDOCNO'
      importing
        number      = lv_apino.
    if sy-subrc <> 0.
      ls_return-type = 'E'.
      ls_return-message = 'Unable To Get Document No.'.
      ex_return = ls_return.
      return.
    endif.
    sort lt_wavezone by matnr itemno picked.


    data(lt_data1) = it_data[].
    sort lt_data1 by crate .
    delete adjacent duplicates from lt_data1 comparing crate .

    loop at lt_data1 into data(ls_data1).
      update zwm_crate set msa_empty = 'X' lgpla = '' ebeln = '' flr_empty = 'X' where crate = ls_data1-crate and lgtyp = lv_lgtyp.
* SOC Update LZONE with 'X' while GRT Picking - LOHIT by - KRANA
      update lagp set : lzone = 'X'
                      uname = sy-uname
                      laedt = sy-datum
                      kober = ' '
                where lgnum = 'V2R'
                  and lgtyp = lv_lgtyp
                  and lgpla =  ls_data1-bin.
* EOC Update LZONE with 'X' while GRT Picking - LOHIT by - KRANA
      commit work and wait.
    endloop.
    loop at it_data assigning <lfs_data>.

      do <lfs_data>-scan_qty times.
        lv_posnr = lv_posnr + 1.

        read table lt_wavezone assigning <lfs_zone> with key matnr = <lfs_data>-material
        picked = ''.
        if sy-subrc eq 0.
          <lfs_zone>-picked = 'X'.
          <lfs_zone>-picked_date = sy-datum.
          <lfs_zone>-picked_to = lv_tanum.
          <lfs_zone>-picked_time = sy-uzeit.
          <lfs_zone>-picked_user = im_user.
          ls_picklist-docno = lv_apino.
          ls_picklist-posnr = lv_posnr.
          ls_picklist-lgnum = 'V2R'.
          ls_picklist-erdat = sy-datum.
          ls_picklist-erzet = sy-uzeit.
          ls_picklist-werks = <lfs_zone>-dwerks.
          ls_picklist-crate = <lfs_data>-crate.
          ls_picklist-process = 'ECOM' ."'GRT'.
          ls_picklist-destination = 'ECOM'. " 'GRT'.
          ls_picklist-matnr = <lfs_zone>-matnr.
          ls_picklist-menge = 1.
          ls_picklist-picklistno = lv_tanumzone.
          ls_picklist-ernam = sy-uname.
          append ls_picklist to lt_picklist.
          clear ls_picklist.
        endif.
      enddo.
    endloop.

    if lt_picklist[] is not initial.
      modify zgrt_picklist from table lt_picklist.
      commit work and wait.
      if lt_wavezone[] is not initial.
        update zwm_wave_zone from table lt_wavezone.
        update zwm_wave_zone_f from table lt_wavezone.
        commit work and wait.
      endif.
      ex_tanum = lv_tanum.
      ls_return-type = 'S'.
      ls_return-message_v1 = lv_tanum.
      concatenate 'SAP-Data Saved Successfully With TO Number : ' lv_tanum into ls_return-message.
      ex_return = ls_return.
    endif.
  endif.
*ENDIF.

endfunction.