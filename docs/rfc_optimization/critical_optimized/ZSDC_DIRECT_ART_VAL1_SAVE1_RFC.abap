
FUNCTION ZSDC_DIRECT_ART_VAL1_SAVE1_RFC.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_STORE_CODE) TYPE  WERKS_D OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_DATA TYPE  ZSDC_FLRBARCODE_TT OPTIONAL
*"----------------------------------------------------------------------
  DATA : "RETURN_TAB       TYPE TABLE OF DDSHRETVAL,
    LT_LTAP_CREAT    TYPE STANDARD TABLE OF LTAP_CREAT,
    E_TANUM          TYPE LTAK-TANUM,
*         GOODSMVT_HEADRET LIKE BAPI2017_GM_HEAD_RET,
    MATERIALDOCUMENT TYPE BAPI2017_GM_HEAD_RET-MAT_DOC,
    MATDOCUMENTYEAR  TYPE BAPI2017_GM_HEAD_RET-DOC_YEAR,
    LT_GOODSMVT_ITEM TYPE TABLE OF BAPI2017_GM_ITEM_CREATE,
    RETURN           TYPE TABLE OF BAPIRET2,
    GOODSMVT_HEADER  TYPE BAPI2017_GM_HEAD_01,
    GOODSMVT_CODE    TYPE BAPI2017_GM_CODE.
**         MAT              TYPE MARM-EAN11.

  IF IM_USER IS INITIAL.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'USER ID SHOULD NOT BE BLANK' ).
    RETURN.
  ENDIF.

  IF IM_STORE_CODE IS INITIAL.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'STORE CODE SHOULD NOT BE BALNK' ).
    RETURN.
  ENDIF.

  SELECT SINGLE BCODE FROM ZWM_USR02 INTO @DATA(LV_PLANT)
     WHERE BNAME = @IM_USER
       AND WERKS = @IM_STORE_CODE.
  IF SY-SUBRC NE 0.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'PLANT IS NOT MATCH WITH THE USER' ).
    RETURN.
  ENDIF.

  LOOP AT ET_DATA ASSIGNING FIELD-SYMBOL(<LFS_DATA>).
    IF <LFS_DATA>-SCAN_QTY GT <LFS_DATA>-VERME.
      EX_RETURN = VALUE #( TYPE = 'E'
                           MESSAGE = 'QUANTITY EXCEEDED') .
      RETURN.
    ENDIF.
  ENDLOOP.


  IF ET_DATA[] IS NOT INITIAL.
    LT_GOODSMVT_ITEM = VALUE #( FOR LS_DATA IN ET_DATA
                              ( MOVE_TYPE       = '311'
                                MOVE_STLOC      = '0001'
                                STGE_LOC        = '0002'
                                PLANT           = LS_DATA-WERKS
                                ENTRY_QNT       = LS_DATA-SCAN_QTY
                                MATERIAL        = LS_DATA-MATNR )
                                ).

    LT_LTAP_CREAT   = VALUE #( FOR L_DATA IN ET_DATA
                             ( MATNR          = L_DATA-MATNR
                               WERKS          = L_DATA-WERKS
                               LGORT          = '0002'
                               ANFME          = L_DATA-SCAN_QTY
                               VLTYP          = 'V09'
                               VLPLA          = L_DATA-FLOOR_BIN
                               NLTYP          = 'V04'
                               NLPLA          = 'IN-TRANSIT'
                               ALTME          = 'EA'
                               SQUIT          = 'X'
                              ) ).


*** DELETE EMPTY LINES IF ANY
    DELETE LT_GOODSMVT_ITEM WHERE ENTRY_QNT IS INITIAL.
    DELETE LT_LTAP_CREAT WHERE ANFME IS INITIAL.

***    PREPARE HEADER
    CLEAR:GOODSMVT_HEADER,GOODSMVT_CODE,MATERIALDOCUMENT,MATDOCUMENTYEAR.

    GOODSMVT_HEADER-HEADER_TXT  = 'HST-V09 TO 0001'. "'GRT HU - HU NO.'.
    GOODSMVT_HEADER-DOC_DATE    = SY-DATUM.
    GOODSMVT_HEADER-PSTNG_DATE  = SY-DATUM.
    GOODSMVT_CODE-GM_CODE       = '04'.

*** POST GOODS MOVEMENT FOR ALL SCANNED ITEMS
    IF LT_GOODSMVT_ITEM IS NOT INITIAL.
      CALL FUNCTION 'BAPI_GOODSMVT_CREATE'
        EXPORTING
          GOODSMVT_HEADER  = GOODSMVT_HEADER
          GOODSMVT_CODE    = GOODSMVT_CODE
        IMPORTING
***          GOODSMVT_HEADRET = GOODSMVT_HEADRET
          MATERIALDOCUMENT = MATERIALDOCUMENT
          MATDOCUMENTYEAR  = MATDOCUMENTYEAR
        TABLES
          GOODSMVT_ITEM    = LT_GOODSMVT_ITEM
          RETURN           = RETURN.

      IF MATERIALDOCUMENT IS INITIAL.
        CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
        READ TABLE RETURN ASSIGNING FIELD-SYMBOL(<FS_RETURN>) WITH KEY TYPE = 'E'.
        IF SY-SUBRC IS INITIAL.
          EX_RETURN = VALUE #( TYPE     = 'E'
                               MESSAGE  = <FS_RETURN>-MESSAGE ).
          RETURN.
        ENDIF.
      ELSE.

        CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING WAIT = 'X'.
****        WAIT UP TO 1 SECONDS.   ""Commented by Priya Skyper02.04.2026 10:08:47""

        CALL FUNCTION 'DEQUEUE_ALL'
          EXPORTING
            _SYNCHRON = 'X'.

        REFRESH: LT_GOODSMVT_ITEM,RETURN.
        CLEAR:E_TANUM.
        IF LT_LTAP_CREAT IS NOT INITIAL.


          ""02.04.2026 09:45:43 Start Added By Priya .

          LOOP AT ET_DATA INTO DATA(L_DATA_CHECK).
            DATA(LV_VERME) = 0.

            SELECT SUM( VERME )
              INTO @LV_VERME
              FROM LQUA
              WHERE LGNUM = 'SDC'
              AND LGTYP = 'V09'
              AND WERKS = @L_DATA_CHECK-WERKS
              AND LGPLA = @L_DATA_CHECK-FLOOR_BIN
              AND MATNR = @L_DATA_CHECK-MATNR.

            IF LV_VERME IS INITIAL OR LV_VERME < L_DATA_CHECK-SCAN_QTY.
              EX_RETURN = VALUE #( TYPE = 'E'
                                   MESSAGE = |No stock in bin { L_DATA_CHECK-FLOOR_BIN }| ).
              RETURN.
            ENDIF.
          ENDLOOP.


          ""% End Added By Priya.""

          CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
            EXPORTING
              I_LGNUM                = 'SDC'
              I_BWLVS                = '999'
              I_KOMPL                = ''
            IMPORTING
              E_TANUM                = E_TANUM
            TABLES
              T_LTAP_CREAT           = LT_LTAP_CREAT
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
              LGORT_WRONG_OR_MISSING = 38.

          IF E_TANUM IS NOT INITIAL.
            REFRESH : LT_LTAP_CREAT.
            "added by jitendra on 24-mar-26
            CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'
              EXPORTING
                WAIT = 'X'.

            CALL FUNCTION 'DEQUEUE_ALL'
              EXPORTING
                _SYNCHRON = 'X'.

            EX_RETURN = VALUE #(
                          TYPE    = 'S'
                          MESSAGE = |TO and Mat-doc Created - TO:{ E_TANUM } MATDOC:{ MATERIALDOCUMENT }| ).
            RETURN.
          ELSE.
            MESSAGE
                    ID SY-MSGID
                  TYPE SY-MSGTY
                NUMBER SY-MSGNO
                  WITH SY-MSGV1
                       SY-MSGV2
                       SY-MSGV3
                       SY-MSGV4 INTO EX_RETURN-MESSAGE.
            RETURN.
          ENDIF.
          CLEAR:E_TANUM.
        ENDIF.
      ENDIF.
    ENDIF.
  ELSE.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'NO DATA FOUND' ).
    RETURN.
  ENDIF.

ENDFUNCTION.