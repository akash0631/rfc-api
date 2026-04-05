FUNCTION ZWM_STORE_IROD_BIN_POST.
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
    LV_LGPLA  TYPE LGPLA,
    LV_MATNR  TYPE MATNR,
    LV_WERKS  TYPE WERKS_D,
    LV_USER   TYPE WWWOBJID,
    LV_NUMBER TYPE NUMC10,
    LV_TANUM  TYPE TANUM,
    LV_LGNUM  TYPE LGNUM.


  DATA:
    LR_LGPLA TYPE RANGE OF LGPLA.


  LR_LGPLA = VALUE #( ( SIGN = 'I' OPTION = 'EQ' LOW = 'GO-LIVE' )
                      ( SIGN = 'I' OPTION = 'EQ' LOW = 'WE-ZONE' )
                    ( SIGN = 'I' OPTION = 'CP' LOW = '49*' ) ).

  LV_USER = TO_UPPER( |{ IM_USER ALPHA = IN }| ).
  LV_WERKS = IM_WERKS.

  LV_LGNUM = COND #( WHEN IM_LGNUM IS INITIAL THEN 'SDC' ELSE IM_LGNUM ).

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

  DATA(LT_DATA) = IT_DATA[].

  SELECT A~LGNUM,
         A~MATNR,
         A~WERKS,
         D~MAKTX,
         A~LGORT,
         A~LGPLA,
         A~LGTYP,
         A~MEINS,
         A~VERME,
         B~MATKL,
         E~WGBEZ,
    CASE A~LGPLA WHEN 'GO-LIVE' THEN 1 ELSE 2 END AS SORT_SEQ
    FROM LQUA AS A
  INNER JOIN @LT_DATA AS F
     ON LTRIM( F~MATNR, '0' ) = LTRIM( A~MATNR, '0' )
   INNER JOIN MARA AS B
      ON B~MATNR EQ A~MATNR
   INNER JOIN MAKT AS D
      ON D~MATNR EQ A~MATNR
     AND D~SPRAS EQ @SY-LANGU
   INNER JOIN T023T AS E
      ON E~SPRAS EQ @SY-LANGU
     AND E~MATKL EQ B~MATKL
   WHERE A~LGNUM = @LV_LGNUM
     AND A~LGTYP = 'V01'
     AND A~LGORT = '0001'
     AND A~WERKS = @LV_WERKS
     AND A~LGPLA IN @LR_LGPLA  "= 'GO-LIVE'
    INTO TABLE @DATA(LT_LQUA).

  SORT LT_LQUA BY LGNUM MATNR WERKS SORT_SEQ.

  DATA: LT_LTAP_CREATE TYPE STANDARD TABLE OF LTAP_CREAT INITIAL SIZE 0,
        LS_LTAP_CRATE1 TYPE LTAP_CREAT.

  DATA: LS_HUSDC TYPE ZMM_HUSDC,
        LT_VEPO  TYPE STANDARD TABLE OF VEPO,

        LS_MSEG  TYPE MSEG.  "VKS-12.01.2021

  DATA: LV_COUNT TYPE MBLPO.


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


    DATA(LV_INDEX) = LINE_INDEX( LT_LQUA[ LGNUM = LV_LGNUM
                                          MATNR = <LFS_DATA>-MATNR
                                          WERKS = LV_WERKS ] ).

    IF LV_INDEX GT 0.

      LOOP AT LT_LQUA ASSIGNING FIELD-SYMBOL(<LFS_LQUA>) FROM LV_INDEX.

        IF NOT ( <LFS_LQUA>-LGNUM EQ LV_LGNUM AND
                 <LFS_LQUA>-MATNR EQ <LFS_DATA>-MATNR AND
                 <LFS_LQUA>-WERKS EQ LV_WERKS ) .
          EXIT.
        ENDIF.

        IF <LFS_DATA>-VERME GE <LFS_LQUA>-VERME.
          DATA(LV_VERME) = <LFS_LQUA>-VERME.
          <LFS_DATA>-VERME -= <LFS_LQUA>-VERME.
        ELSE.
          LV_VERME = <LFS_DATA>-VERME.
          CLEAR <LFS_DATA>-VERME.
        ENDIF.

        APPEND VALUE #( MATNR = <LFS_DATA>-MATNR
                        WERKS = LV_WERKS
                        LGORT = COND #( WHEN <LFS_DATA>-LGORT IS INITIAL THEN '0001' ELSE <LFS_DATA>-LGORT )
                        ANFME = LV_VERME
                        ALTME = COND #( WHEN <LFS_DATA>-MEINS IS INITIAL THEN 'EA' ELSE <LFS_DATA>-MEINS )
                        SQUIT = ABAP_TRUE
                        VLTYP = <LFS_LQUA>-LGTYP
                        VLPLA = <LFS_LQUA>-LGPLA
                        VLBER = '001'
                        NLTYP = LV_WERKS+1(3)
                        NLPLA = <LFS_DATA>-LGPLA
                        NLBER = '001'  )
                        TO LT_LTAP_CREATE .

        IF <LFS_DATA>-VERME IS INITIAL.
          EXIT.
        ENDIF.

      ENDLOOP.
    ENDIF.

  ENDLOOP.

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
      I_LGNUM                = LV_LGNUM
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

    DATA:
        LT_IROD_MASTER TYPE STANDARD TABLE OF ZWM_ST01_IROD_MC.

    SELECT
  DISTINCT A~WERKS,
           A~IROD,
           C~MAJ_CAT_CD,
           A~TYPE
      FROM ZWM_ST01_IROD_MC AS A
     INNER JOIN @LT_DATA AS B
        ON A~WERKS EQ B~WERKS
       AND A~IROD  EQ B~LGPLA
     INNER JOIN MARA AS M
        ON M~MATNR EQ B~MATNR
     INNER JOIN ZMC_MASTER AS C
        ON C~MATKL EQ M~MATKL
      INTO CORRESPONDING FIELDS OF TABLE @LT_IROD_MASTER.

    DELETE LT_IROD_MASTER WHERE IROD IS INITIAL.

    SORT LT_IROD_MASTER BY WERKS ASCENDING IROD ASCENDING IROD MAJ_CAT_CD DESCENDING.
    DELETE ADJACENT DUPLICATES FROM LT_IROD_MASTER COMPARING WERKS IROD.

    MODIFY ZWM_ST01_IROD_MC FROM TABLE LT_IROD_MASTER.

    RETURN.
  ENDIF.

ENDFUNCTION.