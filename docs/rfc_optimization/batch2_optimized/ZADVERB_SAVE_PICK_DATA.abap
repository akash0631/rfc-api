FUNCTION zadverb_save_pick_data.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_TANUM) TYPE  TANUM OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_TEST) TYPE  CHAR1 OPTIONAL
*"     VALUE(IM_NATURE) TYPE  CHAR1 OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------

  FIELD-SYMBOLS : <lfs_data> TYPE zwm_store_stru.
  DATA : ls_data TYPE zwm_store_stru.
  DATA : lt_ltap_create TYPE STANDARD TABLE OF ltap_creat.
  DATA : lv_tanumzone TYPE tanum.
  DATA : lt_mara TYPE tt_mara,
         ls_mara TYPE ty_mara.
  DATA : ls_return   TYPE bapiret2,
         ls_picklist TYPE zadverb_picklist.
  DATA : ls_ltap_create TYPE ltap_creat .
  DATA : lv_qty     TYPE menge_d,
         lv_tanum   TYPE tanum,
         lv_message TYPE bapi_msg.
  DATA : lt_pick TYPE STANDARD TABLE OF zecom_pick,
         ls_pick LIKE LINE OF lt_pick.
  DATA : lv_apino TYPE zzapino.
  DATA : lv_posnr TYPE posnr.
  DATA : lv_sammg TYPE sammg.
  DATA : lt_data TYPE zwm_store_stru_t.
  DATA : ls_crate TYPE zwm_crate.
  DATA : lt_pickdata TYPE zecom_pick_data_tt.
  CLEAR lv_apino.
  DATA : lt_wavezone TYPE STANDARD TABLE OF zwm_wave_zone,
         ls_wavezone TYPE zwm_wave_zone.
  FIELD-SYMBOLS <lfs_zone> TYPE zwm_wave_zone.
  DATA : lt_picklist TYPE STANDARD TABLE OF zadverb_picklist .

  RANGES : r_wave FOR zwm_wave_zone-wave.

  IF im_user IS INITIAL.
    ls_return-type = 'E'.
    ls_return-message = 'User ID Cannot Be Blank.'.
    ex_return = ls_return.
    RETURN.
  ENDIF.

  IF im_tanum IS INITIAL.
    ls_return-type = 'E'.
    ls_return-message = 'TO Number Cannot Be Blank.'.
    ex_return = ls_return.
    RETURN.
  ELSE.
    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        input  = im_tanum
      IMPORTING
        output = lv_tanumzone.

  ENDIF.
  lt_data[] = it_data[].
  SORT lt_data BY plant crate.
  DELETE ADJACENT DUPLICATES FROM lt_data COMPARING plant crate.

  READ TABLE lt_data WITH KEY crate = '' TRANSPORTING NO FIELDS .
  IF sy-subrc IS INITIAL .
    ls_return-type = 'E'.
    ls_return-message = 'Blank Crate Not Allowed'.
    ex_return = ls_return.
    RETURN.
  ENDIF.

  LOOP AT lt_data ASSIGNING FIELD-SYMBOL(<lfs_data1>).
    <lfs_data1>-plant = 'DH24'.
    TRANSLATE <lfs_data1>-bin TO UPPER CASE.
    TRANSLATE <lfs_data1>-crate TO UPPER CASE.
*    TRANSLATE ls_data-plant TO UPPER CASE.
*    IF ls_data-plant NE 'DH24'.
*      ls_return-type = 'E'.
*      ls_return-message = 'Invalid Plant.'.
*      ex_return = ls_return.
*      RETURN.
*    ENDIF.
*    IF ls_data-crate IS INITIAL.
*      SELECT SINGLE * FROM zwm_crate
*      INTO ls_crate
*      WHERE lgpla = ls_data-bin
*      AND lgnum = 'V2R'.
*      IF sy-subrc NE 0.
*        ls_return-type = 'E'.
*        CONCATENATE 'Bin : ' ls_data-bin ' Is Not Tagged To A Crate' INTO ls_return-message.
*        ex_return = ls_return.
*        RETURN.
*      ENDIF.
*    ELSE.
*      SELECT SINGLE * FROM zwm_crate
*      INTO ls_crate
*      WHERE crate = ls_data-crate
*      AND lgnum = 'V2R'
*      AND msa_empty = 'X'.
*      IF sy-subrc NE 0.
*        ls_return-type = 'E'.
*        ls_return-message = 'Invalid Crate Or Crate Is Not Empty.'.
*        ex_return = ls_return.
*        RETURN.
*      ENDIF.
*    ENDIF.
  ENDLOOP.

  REFRESH lt_data[].
  lt_data[] = it_data[].
  IF lt_data[] IS NOT INITIAL.
    SELECT lgnum zone lgtyp lgpla
      FROM ZWM_WAVE_ZONE
    INTO TABLE lt_wavezone
    FOR ALL ENTRIES IN lt_data
    WHERE tanum = lv_tanumzone
    AND   lgpla = lt_data-bin
    AND   picked = ''
    AND etype = 'PTL'.

    SELECT DISTINCT 'I', 'EQ' , wave FROM @lt_wavezone AS a INTO TABLE @r_wave .

    SELECT lgnum zone lgtyp lgpla
      FROM ZWM_WAVE_ZONE_F
    APPENDING TABLE lt_wavezone
    FOR ALL ENTRIES IN lt_data
    WHERE wave IN r_wave
    AND lgpla = lt_data-bin
    AND   picked = ''.

  ENDIF.

  IF lt_wavezone IS NOT INITIAL.
    SELECT matnr meins
    FROM mara
    INTO TABLE lt_mara
    FOR ALL ENTRIES IN lt_wavezone
    WHERE matnr = lt_wavezone-matnr.
    SORT lt_mara BY matnr.
  ENDIF.


  REFRESH it_data.

  LOOP AT lt_wavezone INTO ls_wavezone.
    ls_data-material = ls_wavezone-matnr.
    ls_data-plant = 'DH24'.
    READ TABLE lt_mara INTO ls_mara WITH KEY matnr = ls_wavezone-matnr BINARY SEARCH.
    IF sy-subrc EQ 0.
      ls_data-meins = ls_mara-meins.
    ENDIF.
    ls_data-stor_loc = '0001'.
    ls_data-bin = ls_wavezone-lgpla.
    ls_data-crate = ls_wavezone-crate.
    ls_data-sammg = ls_wavezone-wave.
    ls_data-scan_qty = ls_wavezone-menge.
    APPEND ls_data TO it_data.
    CLEAR ls_mara.
    CLEAR ls_data.
  ENDLOOP.


  SORT lt_wavezone BY tanum itemno vbeln_vl.

  CHECK im_test IS INITIAL.

  LOOP AT  it_data ASSIGNING <lfs_data>.
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
    ls_ltap_create-nltyp = 'V07'.                      "Destination Storage Type
    ls_ltap_create-nlpla = 'ADVERB'. "<lfs_data>-crate.           "Destination Storage Bin
    ls_ltap_create-vltyp = 'V06'.                      "Source Storage Type
    ls_ltap_create-vlpla = <lfs_data>-sammg.           "Source Bin
    ls_ltap_create-zeugn = lv_tanumzone.
    COLLECT ls_ltap_create INTO lt_ltap_create.
    IF lv_sammg IS INITIAL.
      lv_sammg = <lfs_data>-sammg.
    ENDIF.
    CLEAR ls_ltap_create.
  ENDLOOP.

  CHECK lt_ltap_create[] IS NOT INITIAL .
  CALL FUNCTION 'DEQUEUE_ALL'
    EXPORTING
      _synchron = 'X'.

  CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
    EXPORTING
      i_lgnum                = 'V2R'
      i_bwlvs                = '853'
      i_kompl                = ''
      i_betyp                = 'G'
      i_benum                = lv_sammg "is_vbsk-sammg
    IMPORTING
      e_tanum                = lv_tanum
    TABLES
      t_ltap_creat           = lt_ltap_create
    EXCEPTIONS
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
      OTHERS                 = 39.
  IF sy-subrc <> 0.

    CALL FUNCTION 'FORMAT_MESSAGE'
      EXPORTING
        id        = sy-msgid
        lang      = sy-langu
        no        = sy-msgno
        v1        = sy-msgv1
        v2        = sy-msgv2
        v3        = sy-msgv3
        v4        = sy-msgv4
      IMPORTING
        msg       = lv_message
      EXCEPTIONS
        not_found = 1
        OTHERS    = 2.
    ls_return-type = 'E'.
    ls_return-message = lv_message.
    ex_return = ls_return.
    RETURN.
  ELSE.
    lv_message = ''.
    lv_posnr = 000000.
    CALL FUNCTION 'NUMBER_GET_NEXT'
      EXPORTING
        nr_range_nr = '06'
        object      = 'ZZDOCNO'
      IMPORTING
        number      = lv_apino.
    IF sy-subrc <> 0.
      ls_return-type = 'E'.
      ls_return-message = 'Unable To Get Document No.'.
      ex_return = ls_return.
      RETURN.
    ENDIF.
    SORT lt_wavezone BY matnr itemno picked.

    DATA(lt_data1) = it_data[].
    SORT lt_data1 BY crate .
    DELETE ADJACENT DUPLICATES FROM lt_data1 COMPARING crate .

    LOOP AT lt_data1 INTO DATA(ls_data1).
      UPDATE zwm_crate SET msa_empty = 'X' lgpla = '' ebeln = '' flr_empty = 'X' WHERE crate = ls_data1-crate.
      COMMIT WORK AND WAIT.
    ENDLOOP.

    LOOP AT it_data ASSIGNING <lfs_data>.
*      IF sy-tabix = 1.
*        UPDATE zwm_crate SET msa_empty = 'X' lgpla = '' ebeln = '' flr_empty = 'X' WHERE crate = <lfs_data>-crate.
*        COMMIT WORK AND WAIT.
*      ENDIF.

      DO <lfs_data>-scan_qty TIMES.
        lv_posnr = lv_posnr + 1.

        READ TABLE lt_wavezone ASSIGNING <lfs_zone> WITH KEY matnr = <lfs_data>-material
        picked = ''.
        IF sy-subrc EQ 0.
          <lfs_zone>-picked = 'X'.
          ls_picklist-docno = lv_apino.
          ls_picklist-posnr = lv_posnr.
          <lfs_zone>-picked_date = sy-datum.
          <lfs_zone>-picked_time = sy-uzeit.
          <lfs_zone>-picked_user = im_user.
          ls_picklist-lgnum = 'V2R'.
          ls_picklist-erdat = sy-datum.
          ls_picklist-erzet = sy-uzeit.
          ls_picklist-werks = <lfs_zone>-dwerks.
          ls_picklist-crate = <lfs_zone>-crate . "<lfs_data>-crate.
          ls_picklist-process = 'PTL'.
          ls_picklist-destination = 'PTL'.
          ls_picklist-matnr = <lfs_zone>-matnr.
          ls_picklist-menge = 1.
          ls_picklist-picklistno = lv_tanumzone.
          ls_picklist-ernam = sy-uname.
          APPEND ls_picklist TO lt_picklist.
          CLEAR ls_picklist.
        ENDIF.
      ENDDO.
    ENDLOOP.

*    DATA(lt_data1) = it_data[] .
*    SORT lt_data1 BY crate .
*    DELETE ADJACENT DUPLICATES FROM lt_data1 COMPARING crate .
*    LOOP AT lt_data1 ASSIGNING <lfs_data>.
*
*      UPDATE zwm_crate SET msa_empty = '' WHERE crate = <lfs_data>-crate.
*      COMMIT WORK AND WAIT.
*    ENDLOOP.

    IF lt_picklist[] IS NOT INITIAL.

      MODIFY zadverb_picklist FROM TABLE lt_picklist.
      COMMIT WORK AND WAIT.
      IF lt_wavezone[] IS NOT INITIAL.
        UPDATE zwm_wave_zone FROM TABLE lt_wavezone.
        UPDATE zwm_wave_zone_f FROM TABLE lt_wavezone.
        COMMIT WORK AND WAIT.
      ENDIF.
      ex_tanum = lv_tanum.
      ls_return-type = 'S'.
      ls_return-message_v1 = lv_tanum.
      CONCATENATE 'SAP-Data Saved Successfully With TO Number : ' lv_tanum INTO ls_return-message.
      ex_return = ls_return.
    ENDIF.
  ENDIF.
*ENDIF.

ENDFUNCTION.