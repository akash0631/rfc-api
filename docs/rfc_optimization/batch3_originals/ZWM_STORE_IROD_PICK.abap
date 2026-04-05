FUNCTION ZWM_STORE_IROD_PICK.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_BIN_T OPTIONAL
*"----------------------------------------------------------------------

  DATA:
    LV_LGPLA   TYPE LGPLA,
    LV_MATNR   TYPE MATNR,
    LV_WERKS   TYPE WERKS_D,
    LV_USER    TYPE WWWOBJID,
    LV_NUMBER  TYPE NUMC10,
    LV_TANUM   TYPE TANUM,
    LV_QTY     TYPE MENGE_D,
    LV_DIFF    TYPE MENGE_D,
    LV_MSA_QTY TYPE MENGE_D,
    lv_v04_qty TYPE MENGE_D.


  DATA: LT_LTAP_CREATE TYPE STANDARD TABLE OF LTAP_CREAT INITIAL SIZE 0,
        LT_VEPO        TYPE TAB_VEPO,
        LS_MSEG        TYPE MSEG.

  DATA(LT_DATA) = IT_DATA[].

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

  SELECT A~LGNUM,
         A~MATNR,
         A~WERKS,
         A~CHARG,
         A~LGORT,
         A~LGTYP,
         A~LGPLA,
         A~VERME,
         A~MEINS
    FROM LQUA AS A
   INNER JOIN @LT_DATA AS B
      ON A~LGNUM EQ @IM_LGNUM
     AND A~WERKS EQ @LV_WERKS
     AND A~LGTYP EQ @LV_WERKS+1(03)
     AND A~LGORT EQ '0001'
     AND A~LGPLA EQ B~IROD
     INTO TABLE @DATA(LT_LQUA).


  SORT LT_LQUA BY LGNUM MATNR WERKS LGPLA.

  LOOP AT LT_DATA ASSIGNING FIELD-SYMBOL(<LFS_DATA>).

    CLEAR:
      LV_QTY,
      LV_MSA_QTY,
      lv_v04_qty.

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

    READ TABLE LT_LQUA ASSIGNING FIELD-SYMBOL(<LFS_LQUA>)
        WITH KEY LGNUM = IM_LGNUM
                 MATNR = <LFS_DATA>-MATNR
                 WERKS = LV_WERKS
                 LGPLA = <LFS_DATA>-IROD BINARY SEARCH.

    IF SY-SUBRC IS INITIAL.
      LV_QTY = <LFS_LQUA>-VERME.
      CLEAR <LFS_LQUA>-MATNR.
    ENDIF.

    IF <LFS_DATA>-VERME LE LV_QTY.
      LV_MSA_QTY = <LFS_DATA>-VERME .
    ELSE.
      LV_MSA_QTY  = LV_QTY.
      lv_v04_qty = <LFS_DATA>-VERME - LV_QTY.
    ENDIF.

    IF LV_MSA_QTY IS NOT INITIAL.

      APPEND VALUE #( MATNR = <LFS_DATA>-MATNR
                      WERKS = LV_WERKS
                      LGORT = COND #( WHEN <LFS_DATA>-LGORT IS INITIAL THEN '0001' ELSE <LFS_DATA>-LGORT )
                      ANFME = LV_MSA_QTY "<LFS_DATA>-VERME
                      ALTME = COND #( WHEN <LFS_DATA>-MEINS IS INITIAL THEN 'EA' ELSE <LFS_DATA>-MEINS ) "<LFS_DATA>-MEINS
                      SQUIT = ABAP_TRUE
                      VLTYP = COND #( WHEN <LFS_DATA>-VLTYP IS INITIAL THEN LV_WERKS+1(3) ELSE <LFS_DATA>-VLTYP )
                      VLPLA = <LFS_DATA>-IROD
                      VLBER = '001'
                      NLTYP = COND #( WHEN <LFS_DATA>-LGTYP IS INITIAL THEN 'V11' ELSE <LFS_DATA>-LGTYP )
                      NLPLA = 'BASKET'
                      NLBER = '001'  )
                      TO LT_LTAP_CREATE .
    ENDIF.

    IF lv_v04_qty IS NOT INITIAL.

      APPEND VALUE #( MATNR = <LFS_DATA>-MATNR
                      WERKS = LV_WERKS
                      LGORT = COND #( WHEN <LFS_DATA>-LGORT IS INITIAL THEN '0001' ELSE <LFS_DATA>-LGORT )
                      ANFME = lv_v04_qty "<LFS_DATA>-VERME
                      ALTME = COND #( WHEN <LFS_DATA>-MEINS IS INITIAL THEN 'EA' ELSE <LFS_DATA>-MEINS ) "<LFS_DATA>-MEINS
                      SQUIT = ABAP_TRUE
                      VLTYP = 'V04'
                      VLPLA = 'TRANSFER'
                      VLBER = '001'
                      NLTYP = COND #( WHEN <LFS_DATA>-LGTYP IS INITIAL THEN 'V11' ELSE <LFS_DATA>-LGTYP )
                      NLPLA = 'BASKET'
                      NLBER = '001'  )
                      TO LT_LTAP_CREATE .
    ENDIF.

  ENDLOOP.

  DELETE LT_LQUA WHERE MATNR IS INITIAL.

*  LOOP AT LT_LQUA ASSIGNING <LFS_LQUA>.
*
*    APPEND VALUE #( MATNR = <LFS_LQUA>-MATNR
*                    WERKS = <LFS_LQUA>-WERKS
*                    LGORT = <LFS_LQUA>-LGORT
*                    ANFME = <LFS_LQUA>-VERME
*                    ALTME = <lfs_lqua>-MEINS
*                    SQUIT = ABAP_TRUE
*                    VLTYP = <lfs_lqua>-lgtyp
*                    VLPLA = <lfs_lqua>-lgpla
*                    VLBER = '001'
*                    NLTYP = 'V11'
*                    NLPLA = <lfs_lqua>-lgpla
*                    NLBER = '001'  )
*                    TO LT_LTAP_CREATE .
*
*  ENDLOOP.

  PERFORM F_SAVE_TEMP_DATA USING SPACE
                                 LT_LTAP_CREATE
                                 LT_VEPO
                                 LV_WERKS
                                 LV_USER
                                 SPACE
                                 '0001'
                                 '0001'
                                 LV_NUMBER
                                 LS_MSEG
                                 '5'.

  CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
    EXPORTING
      I_LGNUM                = IM_LGNUM
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

    EX_RETURN-TYPE = C_ERROR.

    UPDATE ZWM_STORE_HST SET: MSG = EX_RETURN-MESSAGE
                              WHERE HST_NR = LV_NUMBER AND DOC_TYPE = '5'.
    MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
          WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4 INTO EX_RETURN-MESSAGE.

  ELSE.
    UPDATE ZWM_STORE_HST SET: TANUM = LV_TANUM MBLNR = '9999999999' MJAHR = '9999'
                              WHERE HST_NR = LV_NUMBER AND DOC_TYPE = '5'.

    CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'
      EXPORTING
        WAIT = 'X'.

    CALL FUNCTION 'DEQUEUE_ALL'
      EXPORTING
        _SYNCHRON = 'X'.

    EX_RETURN-TYPE = C_SUCCESS.
    CONCATENATE LV_TANUM ' ' 'Created' INTO EX_RETURN-MESSAGE.
*    MESSAGE S016(L3) WITH LV_TANUM INTO EX_RETURN-MESSAGE.
*    EX_TANUM = LV_TANUM.
    RETURN.
  ENDIF.

ENDFUNCTION.