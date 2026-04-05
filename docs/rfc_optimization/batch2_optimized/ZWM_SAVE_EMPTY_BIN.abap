FUNCTION ZWM_SAVE_EMPTY_BIN.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_LGNUM) TYPE  LGNUM OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA TYPE  ZWM_BIN_SCAN_T OPTIONAL
*"----------------------------------------------------------------------
  BREAK-POINT ID Z_V2CHECK.
  DATA: LT_CRATE  TYPE STANDARD TABLE OF ZWM_CRATE,
        LS_CRATE  TYPE ZWM_CRATE,
        LT_CRATE1 TYPE STANDARD TABLE OF ZWM_CRATE,
        LT_LAGP   TYPE STANDARD TABLE OF LAGP,
        LS_CRATE1 TYPE ZWM_CRATE,
        LV_LGNUM  TYPE LGNUM.

  DATA: LT_DATA  TYPE ZWM_BIN_SCAN_T,
        LT_ITEM  TYPE STANDARD TABLE OF ZWM_ITEM_PUTWAY,
        LS_DATA  TYPE ZWM_BIN_SCAN,

        LV_TANUM TYPE TANUM.

  FIELD-SYMBOLS: <LFS_LAGP> TYPE LAGP,
                 <LFS_ITEM> TYPE ZWM_ITEM_PUTWAY.

  DATA: LT_LTAP_CREATE TYPE STANDARD TABLE OF LTAP_CREAT INITIAL SIZE 0.

*lv_lgnum = 'V2R'.    "Commented by Vaibhav
*
*
*if im_werks = 'DH24'.
*  data(lv_lgtyp) = 'E01'.
*  else.
*
*  lv_lgtyp = 'E02'.
*endif.


  """"""""""""""""""""""""""""""""""""""""""""""""""""""""""""""""""""""" "added by vaibhav12.11.2025 10:34:27
  "ADDED LV_NLTYP BY JITENDRA
  SELECT SINGLE LGTYP, LGNUM, NLTYP FROM ZWM_DC_MASTER WHERE WERKS = @IM_WERKS
                        INTO ( @DATA(LV_LGTYP), @LV_LGNUM , @DATA(LV_NLTYP) ) . "ADDED BY JITENDRA SKYPER 04.02.2026 09:53:36


  IF IT_DATA[] IS NOT INITIAL.
    SORT IT_DATA .
    DELETE ADJACENT DUPLICATES FROM IT_DATA COMPARING ALL FIELDS.

    PERFORM GET_LQUA_DATA  USING 'V2R'
                                 IM_WERKS
                                 IT_DATA[]
                                 LT_LTAP_CREATE.

    IF LT_LTAP_CREATE[] IS NOT INITIAL.
      SORT LT_LTAP_CREATE BY VLPLA.
      CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
        EXPORTING
          I_LGNUM                = 'V2R'
          I_BWLVS                = '999'
          I_COMMIT_WORK          = 'X'
          I_KOMPL                = ''
        IMPORTING
          E_TANUM                = LV_TANUM
        TABLES
          T_LTAP_CREAT           = LT_LTAP_CREATE
        EXCEPTIONS
          NO_TO_CREATED          = 1
          BWLVS_WRONG            = 2
          BETYP_WRONG            = 3
          BENUM_MISSING          = 4
          BETYP_MISSING          = 5
          FOREIGN_LOCK           = 6
          VLTYP_WRONG            = 7
          VLPLA_WRONG            = 8
          VLTYP_MISSING          = 9
          NLTYP_WRONG            = 10
          NLPLA_WRONG            = 11
          NLTYP_MISSING          = 12
          RLTYP_WRONG            = 13
          RLPLA_WRONG            = 14
          RLTYP_MISSING          = 15
          SQUIT_FORBIDDEN        = 16
          MANUAL_TO_FORBIDDEN    = 17
          LETYP_WRONG            = 18
          VLPLA_MISSING          = 19
          NLPLA_MISSING          = 20
          SOBKZ_WRONG            = 21
          SOBKZ_MISSING          = 22
          SONUM_MISSING          = 23
          BESTQ_WRONG            = 24
          LGBER_WRONG            = 25
          XFELD_WRONG            = 26
          DATE_WRONG             = 27
          DRUKZ_WRONG            = 28
          LDEST_WRONG            = 29
          UPDATE_WITHOUT_COMMIT  = 30
          NO_AUTHORITY           = 31
          MATERIAL_NOT_FOUND     = 32
          LENUM_WRONG            = 33
          MATNR_MISSING          = 34
          WERKS_MISSING          = 35
          ANFME_MISSING          = 36
          ALTME_MISSING          = 37
          LGORT_WRONG_OR_MISSING = 38
          OTHERS                 = 39.

      IF SY-SUBRC <> 0.
* MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
* WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.
      ENDIF.

      IF LV_TANUM IS INITIAL.
        MESSAGE 'TO not created' TYPE 'E'.
      ENDIF.
    ENDIF.

    LT_DATA[] = IT_DATA[].
    SORT LT_DATA BY BIN .
    DELETE ADJACENT DUPLICATES FROM LT_DATA COMPARING BIN.

    LOOP AT LT_DATA INTO LS_DATA.
*      if lv_lgnum = 'V2R' and ls_data-bin_type = 'E01'.
*        update lagp set : lzone = 'X'
*                          uname = sy-uname laedt = sy-datum
*                          kober = ' ' " update picking area
*                    where lgnum = lv_lgnum
*                      and lgpla  = ls_data-bin.
*      else.
      UPDATE LAGP SET LZONE = 'X'
                      UNAME = SY-UNAME LAEDT = SY-DATUM
                      KOBER = ' ' " update picking area
                      LPTYP = '' "ADDED BY JITENDRA SKYPER 04.02.2026 09:53:20
                WHERE LGNUM = LV_LGNUM
                  AND LGTYP IN ( LV_LGTYP, LV_NLTYP ) "ADDED BY JITENDRA SKYPER 04.02.2026 09:53:26
***                  AND LGTYP = LV_LGTYP
                  AND LGPLA  = LS_DATA-BIN.
*      endif.
      CLEAR LS_DATA .
    ENDLOOP.

    SELECT * FROM ZWM_CRATE INTO TABLE LT_CRATE
                    FOR ALL ENTRIES IN IT_DATA
                                 WHERE LGNUM = LV_LGNUM
                                    AND LGTYP = LV_LGTYP
                                   AND LGPLA  = IT_DATA-BIN.
    IF SY-SUBRC IS INITIAL.
      LOOP AT LT_CRATE INTO LS_CRATE.
        MOVE-CORRESPONDING LS_CRATE TO LS_CRATE1.
        LS_CRATE1-ERNAM = IM_USER.
        LS_CRATE1-UZEIT = SY-UZEIT.
        LS_CRATE1-AEDAT = SY-DATUM.
        LS_CRATE1-LGPLA = ''.
*        ls_crate1-lgtyp = ''.
        LS_CRATE1-LGTYP = LS_CRATE1-LGTYP. " added by rajeev
        LS_CRATE1-EBELN = ''.
        LS_CRATE1-LOCKED = ''.
        LS_CRATE1-MSA_EMPTY = 'X'.
        LS_CRATE1-FLR_EMPTY = 'X'.
        APPEND LS_CRATE1 TO LT_CRATE1.
        CLEAR: LS_CRATE,LS_CRATE1.
      ENDLOOP.

      DELETE ZWM_CRATE FROM TABLE LT_CRATE.  "VKS-05.09.2019
      INSERT ZWM_CRATE FROM TABLE LT_CRATE1. "UPDATE

      SELECT * FROM ZWM_ITEM_PUTWAY INTO TABLE LT_ITEM
        FOR ALL ENTRIES IN LT_CRATE
        WHERE CRATE = LT_CRATE-CRATE
         AND WERKS = IM_WERKS
        AND TO_PROC = ''.
      IF SY-SUBRC IS INITIAL .
        LOOP AT LT_ITEM ASSIGNING <LFS_ITEM>.
          <LFS_ITEM>-TO_PROC = 'X'.
          <LFS_ITEM>-TANUM = '9999999999'.
        ENDLOOP.
        UPDATE ZWM_ITEM_PUTWAY FROM TABLE LT_ITEM.
      ENDIF.
    ENDIF.

    EX_RETURN-MESSAGE = 'Data has been saved successfully'.
    EX_RETURN-TYPE = 'S'.
    MESSAGE 'Data has been saved' TYPE 'S'.
    RETURN.
  ELSE.
    EX_RETURN-MESSAGE = 'Scan atleast one bin'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.
ENDFUNCTION.