FUNCTION zgrt_pick_validate_hu.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_EXT_HU) TYPE  EXIDV OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_STORECODE) TYPE  WERKS_D
*"  TABLES
*"      ET_DATA TYPE  ZLQUA_TT OPTIONAL
*"      ET_EAN_DATA STRUCTURE  MARM OPTIONAL
*"----------------------------------------------------------------------
***  DATA: ls_exref  TYPE zwm_exref,
***        ls_data   TYPE zlqua_st,
***        lt_data   TYPE zlqua_tt,
***        ls_return TYPE bapiret2.
***  DATA: lv_hu TYPE exidv.
***  DATA : r_matnr TYPE RANGE OF matnr.
***  DATA:
***    lr_mean TYPE REF TO mean,
***    lr_ean  TYPE REF TO marm,
***    lr_marm TYPE REF TO marm,
***    lt_marm TYPE STANDARD TABLE OF marm INITIAL SIZE 0.
***  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
***    EXPORTING
***      input  = im_ext_hu
***    IMPORTING
***      output = lv_hu.
***
***
***  IF im_user IS INITIAL.
***    ls_return-type = 'E'.
***    ls_return-message = 'User ID Cannot Be Blank'.
***    ex_return = ls_return.
***    RETURN.
***  ENDIF.
***
***  IF im_ext_hu IS INITIAL.
***    ls_return-type = 'E'.
***    ls_return-message = 'External HU Cannot Be Blank'.
***    ex_return = ls_return.
***    RETURN.
***  ENDIF.
***
***
***  SELECT SINGLE *
***    FROM zwm_exref
***    WHERE exidv = @lv_hu
***      AND sap_hu IS INITIAL
***    INTO @ls_exref .
***  IF sy-subrc  IS NOT INITIAL.
***    ex_return-type = 'E'.
***    ex_return-message = 'Invalid HU.'.
***    RETURN .
***  ENDIF.
***
****      SELECT lgnum, lqnum, matnr, werks, lgort, lgpla, lgtyp, verme
****        FROM lqua
****        INTO TABLE @DATA(lt_data)
****         WHERE lgnum = 'V2R'
****          AND  werks = 'DH24'
****          AND  lgtyp = 'V07'.
***
***  SELECT * FROM zwm_wave_zone
***            INTO TABLE @DATA(lt_wave)
***                WHERE dwerks = @ls_exref-dwerks
***                  AND mapped = 'X'
***                  AND ex_hu = ''.
***
***
****  loop at lt_wave INTO data(ls_wave).
****      ls_data-lgnum = 'V2R'.
****      ls_data-lqnum = '' .
****      ls_data-matnr = ls_wave-matnr .
****      ls_data-werks = ls_wave-dwerks .
****      ls_data-lgort = '0001' .
****      ls_data-lgpla = ls_wave-lgpla .
****      ls_data-lgtyp = ls_wave-lgtyp .
****      ls_data-verme = ls_wave-menge .
****      COLLECT ls_data INTO lt_data.
****      CLEAR ls_data .
****    ENDLOOP.
***
***  SELECT
***     CAST( 'V2R' AS CHAR( 3 ) ) AS lgnum,
***     matnr,
***     dwerks AS werks,
***     CAST( '0001' AS CHAR( 4 ) ) AS lgort,
***     lgpla,
***     lgtyp,
***     SUM( menge ) AS verme
***   FROM @lt_wave AS a
***    GROUP BY matnr,
***             dwerks,
***             lgpla,
***             lgtyp
***     INTO CORRESPONDING FIELDS OF TABLE @lt_data.
***
***  IF lt_data[] IS NOT INITIAL.
***    et_data[] = CORRESPONDING #( lt_data ).
***    SELECT DISTINCT 'I' ,'EQ',matnr
***      FROM @lt_data AS l
***      INTO TABLE @r_matnr.
***
***    SELECT a~matnr,
***           a~meinh,
***           a~umrez,
***           a~umren,
***           a~eannr,
***           a~ean11
***      FROM marm AS a
***      WHERE matnr IN @r_matnr
***      UNION
***     SELECT a~matnr,
***            a~meinh,
***            CAST( 1 AS DEC( 5 ) ) AS umrez,
***            CAST( 1 AS DEC( 5 ) ) AS umren,
***            CAST( @space AS CHAR( 13 ) ) AS eannr,
***            a~ean11
***       FROM mean AS a
***      WHERE matnr IN @r_matnr
***      INTO CORRESPONDING FIELDS OF TABLE @et_ean_data.
***
***    IF lines( et_ean_data ) EQ 0.
***      ex_return-type = 'E'.
***      ex_return-message = 'No Data For Scan'.
***      ex_storecode = ls_exref-dwerks.
***    ELSE.
***      DELETE et_ean_data WHERE ean11 = ''.
***      SORT et_ean_data BY matnr ean11.
***      DELETE ADJACENT DUPLICATES FROM et_ean_data COMPARING matnr ean11.
***      ex_return-type = 'S'.
***      ex_return-message = 'Valid'.
***      ex_storecode = ls_exref-dwerks.
***    ENDIF.
***  ENDIF.

   DATA: ls_exref  TYPE zwm_exref,
        ls_data   TYPE zlqua_st,
        lt_data   TYPE zlqua_tt,
        ls_return TYPE bapiret2.
  DATA: lv_hu TYPE exidv.
  DATA : r_matnr TYPE RANGE OF matnr.
  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_ext_hu
    IMPORTING
      output = lv_hu.


  IF im_user IS INITIAL.
    ls_return-type = 'E'.
    ls_return-message = 'User ID Cannot Be Blank'.
    ex_return = ls_return.
    RETURN.
  ENDIF.

  IF im_ext_hu IS INITIAL.
    ls_return-type = 'E'.
    ls_return-message = 'External HU Cannot Be Blank'.
    ex_return = ls_return.
    RETURN.
  ELSE.
    SELECT SINGLE *
      FROM zwm_exref
      WHERE exidv = @lv_hu
        AND sap_hu IS INITIAL
      INTO @ls_exref .
    IF sy-subrc EQ 0.
      ex_return-type = 'S'.
      ex_return-message = 'Valid'.
      ex_storecode = ls_exref-dwerks.

*      SELECT lgnum, lqnum, matnr, werks, lgort, lgpla, lgtyp, verme
*        FROM lqua
*        INTO TABLE @DATA(lt_data)
*         WHERE lgnum = 'V2R'
*          AND  werks = 'DH24'
*          AND  lgtyp = 'V07'.

      SELECT * FROM zwm_wave_zone
                INTO TABLE @DATA(lt_wave)
                    WHERE dwerks = @ls_exref-dwerks
                      AND mapped = 'X'
                      AND ex_hu = ''.

      LOOP AT lt_wave INTO DATA(ls_wave).
        ls_data-lgnum = 'V2R'.
        ls_data-lqnum = '' .
        ls_data-matnr = ls_wave-matnr .
        ls_data-werks = ls_wave-dwerks .
        ls_data-lgort = '0001' .
        ls_data-lgpla = ls_wave-lgpla .
        ls_data-lgtyp = ls_wave-lgtyp .
        ls_data-verme = ls_wave-menge .
        COLLECT ls_data INTO lt_data.
        CLEAR ls_data .
      ENDLOOP.

      IF lt_data[] IS NOT INITIAL .
        SELECT DISTINCT 'I' ,'EQ',matnr
          FROM @lt_data AS l
          INTO TABLE @r_matnr.


        SELECT * FROM marm AS m
          INTO TABLE @DATA(lt_ean_data)
          WHERE matnr IN @r_matnr.

          else.
        ex_return-TYPE = 'E'.
        ex_return-MESSAGE = 'No Data For Scan'.
        ex_storecode = ls_exref-dwerks.

      ENDIF.
      et_ean_data[] = lt_ean_data[].
      et_data[] = lt_data[].
      DELETE lt_ean_data WHERE ean11 = ''.
    ELSE.
      ex_return-type = 'E'.
      ex_return-message = 'Invalid HU.'.
    ENDIF.
  ENDIF.



ENDFUNCTION.