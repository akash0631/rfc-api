FUNCTION ZWM_REVERSE_PICK_GRN_TO_RFC.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"     VALUE(EX_MBLNR) TYPE  MBLNR
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_BIN_T OPTIONAL
*"----------------------------------------------------------------------

  DATA:
    LT_RETURN           TYPE BAPIRET2_T,
    LT_GOODSMVT_ITEM    TYPE BAPI2017_GM_ITEM_CREATE_T,
    LT_GOODSMVT_ITEM_2  TYPE BAPI2017_GM_ITEM_CREATE_T,
    LS_GOODSMVT_HEADRET TYPE BAPI2017_GM_HEAD_RET,
    LS_GOODSMVT_HEADER  TYPE BAPI2017_GM_HEAD_01.

  DATA:
    L_MBLNR TYPE MBLNR,
    L_MJAHR TYPE MJAHR.

  DATA:
    LV_LGPLA  TYPE LGPLA,
    LV_MATNR  TYPE MATNR,
    LV_WERKS  TYPE WERKS_D,
    LV_USER   TYPE WWWOBJID,
    LV_NUMBER TYPE NUMC10,
    LV_TANUM  TYPE TANUM.

  LV_USER = TO_UPPER( |{ IM_USER ALPHA = IN }| ).
  LV_WERKS = IM_WERKS.

  IF IM_USER IS INITIAL AND IM_WERKS IS INITIAL.
    EX_RETURN-MESSAGE = 'User Id Cannot Be Blank.'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.

  IF IM_WERKS IS INITIAL.

    SELECT
    SINGLE WERKS
      FROM ZWM_USR02
     WHERE UPPER( BNAME ) EQ @IM_USER
      INTO @LV_WERKS.

    IF SY-SUBRC IS NOT INITIAL.
      EX_RETURN-MESSAGE = 'User Id Cannot Be Blank.'.
      EX_RETURN-TYPE = 'E'.
      RETURN.
    ENDIF.
  ENDIF.


  DATA: LT_LTAP_CREATE  TYPE STANDARD TABLE OF LTAP_CREAT INITIAL SIZE 0,
        LT_LTAP_CREATE2 TYPE STANDARD TABLE OF LTAP_CREAT INITIAL SIZE 0.


  DATA: LS_HUSDC TYPE ZMM_HUSDC,
        LT_VEPO  TYPE STANDARD TABLE OF VEPO,

        LS_MSEG  TYPE MSEG.  "VKS-12.01.2021

  DATA: LV_COUNT TYPE MBLPO.

  DATA(LT_DATA) = IT_DATA[].

  LOOP AT LT_DATA ASSIGNING FIELD-SYMBOL(<LFS_DATA>).

    CALL FUNCTION 'CONVERSION_EXIT_MATN1_INPUT'
      EXPORTING
        INPUT        = <LFS_DATA>-MATNR
      IMPORTING
        OUTPUT       = <LFS_DATA>-MATNR
      EXCEPTIONS
        LENGTH_ERROR = 1.

    IF SY-SUBRC <> 0.
*     MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
*       WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.
    ENDIF.

    DATA(L_TABIX) = SY-TABIX.


    APPEND VALUE #( MOVE_TYPE            = '311'
                    PLANT                = LV_WERKS
                    MOVE_PLANT           = LV_WERKS
                    STGE_LOC             = '0001'
                    MOVE_STLOC           = '0010'
                    ENTRY_QNT            = <LFS_DATA>-VERME
                    ENTRY_UOM            = 'EA' "<LFS_DATA>-MEINS
                    LINE_ID              = L_TABIX
                    MATERIAL_LONG        = <LFS_DATA>-MATNR
                    ) TO LT_GOODSMVT_ITEM.

    APPEND VALUE #( MOVE_TYPE            = '311'
                    PLANT                = LV_WERKS
                    MOVE_PLANT           = LV_WERKS
                    STGE_LOC             = '0010'
                    MOVE_STLOC           = '0002'
                    ENTRY_QNT            = <LFS_DATA>-VERME
                    ENTRY_UOM            = 'EA' "<LFS_DATA>-MEINS
                    LINE_ID              = L_TABIX
                    MATERIAL_LONG        = <LFS_DATA>-MATNR
                    ) TO LT_GOODSMVT_ITEM_2.

    APPEND VALUE #( MATNR = <LFS_DATA>-MATNR
                    WERKS = LV_WERKS
                    LGORT = COND #( WHEN <LFS_DATA>-LGORT IS INITIAL THEN '0001' ELSE <LFS_DATA>-LGORT )
                    ANFME = <LFS_DATA>-VERME
                    ALTME = 'EA' "<LFS_DATA>-MEINS
                    SQUIT = ABAP_TRUE
                    VLTYP = LV_WERKS+1(3)
                    VLPLA = <LFS_DATA>-LGPLA
                    VLBER = '001'
                    NLTYP = 'V04'
                    NLPLA = 'IN-TRANSIT'
                    NLBER = '001' ) TO LT_LTAP_CREATE.

    APPEND VALUE #( MATNR = <LFS_DATA>-MATNR
                 WERKS = LV_WERKS
                 LGORT = COND #( WHEN <LFS_DATA>-LGORT IS INITIAL THEN '0002' ELSE <LFS_DATA>-LGORT )
                 ANFME = <LFS_DATA>-VERME
                 ALTME = 'EA' "<LFS_DATA>-MEINS
                 SQUIT = ABAP_TRUE
                 VLTYP = LV_WERKS+1(3)
                 VLPLA = <LFS_DATA>-LGPLA
                 VLBER = '001'
                 NLTYP = 'V04'
                 NLPLA = 'IN-TRANSIT'
                 NLBER = '001' ) TO LT_LTAP_CREATE2.

  ENDLOOP.

*  PERFORM F_SAVE_TEMP_DATA USING SPACE
*                                 LT_LTAP_CREATE
*                                 LT_VEPO
*                                 LV_WERKS
*                                 LV_USER
*                                 SPACE
*                                 '0001'
*                                 '0010'
*                                 LV_NUMBER
*                                 LS_MSEG
*                                 'T'.

  LS_GOODSMVT_HEADER-HEADER_TXT          = |Revs:{ LV_USER } 'Picking 0001'|.
  LS_GOODSMVT_HEADER-PSTNG_DATE          = SY-DATUM.
  LS_GOODSMVT_HEADER-DOC_DATE            = SY-DATUM.

  CALL FUNCTION 'BAPI_GOODSMVT_CREATE'
    EXPORTING
      GOODSMVT_HEADER  = LS_GOODSMVT_HEADER
      GOODSMVT_CODE    = '04'
    IMPORTING
      GOODSMVT_HEADRET = LS_GOODSMVT_HEADRET
      MATERIALDOCUMENT = L_MBLNR
      MATDOCUMENTYEAR  = L_MJAHR
    TABLES
      GOODSMVT_ITEM    = LT_GOODSMVT_ITEM
      RETURN           = LT_RETURN.



  IF L_MBLNR IS INITIAL.
*    ASSIGN LT_RETURN[ TYPE = 'E' ] TO FIELD-SYMBOL(<LFS_RETURN>).

*    IF <LFS_RETURN> IS ASSIGNED.
*      MESSAGE ID <LFS_RETURN>-ID
*            TYPE <LFS_RETURN>-TYPE
*          NUMBER <LFS_RETURN>-NUMBER
*            WITH <LFS_RETURN>-MESSAGE_V1
*                 <LFS_RETURN>-MESSAGE_V2
*                 <LFS_RETURN>-MESSAGE_V3
*                 <LFS_RETURN>-MESSAGE_V4 INTO EX_RETURN-MESSAGE.

    EX_RETURN-TYPE = 'S'.
    EX_RETURN-MESSAGE =  'Mat Doc 1To10 NOT Created'.
*    ENDIF.

*    UNASSIGN <LFS_RETURN>.
    RETURN.

  ELSE.

    EX_RETURN-TYPE = 'S'.
    EX_RETURN-MESSAGE = |{ L_MBLNR } { 'Mat Doc 1To10 Created'}|.
    UPDATE ZWM_STORE_HST SET: MBLNR = L_MBLNR
                              MJAHR = L_MJAHR
                       WHERE HST_NR = LV_NUMBER
                       AND DOC_TYPE = 'T'.

    CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'
      EXPORTING
        WAIT = 'X'.

    CALL FUNCTION 'DEQUEUE_ALL'
      EXPORTING
        _SYNCHRON = ABAP_TRUE.

    CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
      EXPORTING
        I_LGNUM                = 'SDC'
        I_BWLVS                = '999'
        I_KOMPL                = ABAP_FALSE
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

      EX_RETURN-TYPE = 'S'.
      EX_RETURN-MESSAGE = 'To 1To10 NOT Created'.

      MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
           WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4 INTO EX_RETURN-MESSAGE.
    ELSE.


      EX_RETURN-TYPE = 'S'.
      EX_RETURN-MESSAGE = |{ LV_TANUM } { 'To 1To10 Created'}|.
*      UPDATE ZWM_STORE_HST SET: TANUM = LV_TANUM
*                         WHERE HST_NR = LV_NUMBER
*                           AND DOC_TYPE = 'T'.

      EX_TANUM = LV_TANUM.

*      PERFORM F_SAVE_TEMP_DATA USING SPACE
*                                     LT_LTAP_CREATE
*                                     LT_VEPO
*                                     LV_WERKS
*                                     LV_USER
*                                     SPACE
*                                     '0010'
*                                     '0002'
*                                     LV_NUMBER
*                                     LS_MSEG
*                                     'T'.

      LS_GOODSMVT_HEADER-HEADER_TXT   = 'Return to MSA'.
      LS_GOODSMVT_HEADER-PSTNG_DATE   = SY-DATUM.
      LS_GOODSMVT_HEADER-DOC_DATE     = SY-DATUM.

      CLEAR:
           LT_RETURN,
           L_MBLNR,
           L_MJAHR.

      CALL FUNCTION 'BAPI_GOODSMVT_CREATE'
        EXPORTING
          GOODSMVT_HEADER  = LS_GOODSMVT_HEADER
          GOODSMVT_CODE    = '04'
        IMPORTING
          GOODSMVT_HEADRET = LS_GOODSMVT_HEADRET
          MATERIALDOCUMENT = L_MBLNR
          MATDOCUMENTYEAR  = L_MJAHR
        TABLES
          GOODSMVT_ITEM    = LT_GOODSMVT_ITEM_2
          RETURN           = LT_RETURN.

      IF L_MBLNR IS INITIAL.
*        ASSIGN LT_RETURN[ TYPE = 'E' ] TO <LFS_RETURN>.
        EX_RETURN-TYPE = 'S'.
        EX_RETURN-MESSAGE = 'Doc For 10to2 Not Created'.

*        UNASSIGN <LFS_RETURN>.
      ELSE.
        EX_RETURN-TYPE = 'S'.
        EX_RETURN-MESSAGE = |{ L_MBLNR } { 'Mat Doc 10 to 2 Created' }|.
        EX_MBLNR = L_MBLNR.

*        UPDATE ZWM_STORE_HST SET: MBLNR = L_MBLNR
*                                  MJAHR = L_MJAHR
*                                  TANUM = '9999999999'
*                           WHERE HST_NR = LV_NUMBER
*                          AND DOC_TYPE = 'T'.

        CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'
          EXPORTING
            WAIT = 'X'.

        CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
          EXPORTING
            I_LGNUM                = 'SDC'
            I_BWLVS                = '999'
            I_KOMPL                = ABAP_FALSE
          IMPORTING
            E_TANUM                = LV_TANUM
          TABLES
            T_LTAP_CREAT           = LT_LTAP_CREATE2
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

      EX_RETURN-TYPE = 'S'.
      EX_RETURN-MESSAGE = 'To 1To10 NOT Created'.

      MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
           WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4 INTO EX_RETURN-MESSAGE.
    ELSE.


      EX_RETURN-TYPE = 'S'.
      EX_RETURN-MESSAGE = |{ LV_TANUM } { 'To 1To10 Created'}|.

      EX_TANUM = LV_TANUM.
      endif.

      ENDIF.
    ENDIF.
  ENDIF.



ENDFUNCTION.