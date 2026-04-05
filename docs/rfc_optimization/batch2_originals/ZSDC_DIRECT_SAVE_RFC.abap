FUNCTION ZSDC_DIRECT_SAVE_RFC.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_PLANT) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_HU) TYPE  EXIDV OPTIONAL
*"     VALUE(IM_HU_EMPTY) TYPE  CHAR1 OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA TYPE  ZTT_SDC_ART_VAL OPTIONAL
*"----------------------------------------------------------------------

  DATA : LT_LTAP_CREAT    TYPE STANDARD TABLE OF LTAP_CREAT,
         LS_LTAP_CREATE   TYPE  LTAP_CREAT,
         LT_GOODSMVT_ITEM TYPE TABLE OF BAPI2017_GM_ITEM_CREATE,
         LS_GOODSMVT_ITEM TYPE  BAPI2017_GM_ITEM_CREATE,
         GOODSMVT_HEADRET LIKE  BAPI2017_GM_HEAD_RET,
         MATERIALDOCUMENT TYPE  BAPI2017_GM_HEAD_RET-MAT_DOC,
         MATDOCUMENTYEAR  TYPE  BAPI2017_GM_HEAD_RET-DOC_YEAR,
         E_TANUM          TYPE LTAK-TANUM,
         LV_MESG          TYPE CHAR20,
         GOODSMVT_HEADER  TYPE BAPI2017_GM_HEAD_01,
         GOODSMVT_CODE    TYPE BAPI2017_GM_CODE,
         RETURN           TYPE TABLE OF BAPIRET2,
         LT_DATA          TYPE STANDARD TABLE OF ZSDC_HU_ART.

  TYPES:BEGIN OF ST_FIN,
          EXIDV    TYPE VEKP-EXIDV,
          WERKS    TYPE LQUA-WERKS,
          NAME1    TYPE T001W-NAME1,
          MATNR    TYPE MARM-EAN11,
          FLOOR    TYPE ZSDC_FLRMSTR-FLOOR,
          TQTY     TYPE LQUA-VERME,
          SQTY     TYPE LQUA-VERME,
          PQTY     TYPE LQUA-VERME,
          MBLNR    TYPE MBLNR,
          BWART    TYPE BWART,
          TANUM    TYPE LTAK-TANUM,
          LGPLA    TYPE LGPLA,
          RQTY     TYPE LQUA-VERME,
          FQTY     TYPE LQUA-VERME,
          DIVISION TYPE ZSDC_FLRMSTR-DIVISION,
          EMPTY    TYPE CHAR1,
          UNAME    TYPE UNAME,
          UDATE    TYPE UDATE,
          UZEIT    TYPE UZEIT,
        END OF ST_FIN.

  DATA : LS_HU_ART TYPE ZSDC_HU_ART.
  DATA: LV_HU TYPE VEKP-EXIDV.  ""Added by Priya Skyper""

  IF IM_HU IS INITIAL.
    EX_RETURN = VALUE #( TYPE     = 'E'
                         MESSAGE  = 'Enter HU' ).
    RETURN.
  ENDIF.


  IF IT_DATA[] IS INITIAL .
    EX_RETURN = VALUE #( TYPE     = 'E'
                         MESSAGE  = 'No Item Data to Post ' ).
    RETURN.
  ENDIF.

  READ TABLE IT_DATA WITH KEY BIN = '' TRANSPORTING NO FIELDS .
  IF SY-SUBRC IS INITIAL .
    EX_RETURN = VALUE #( TYPE     = 'E'
                       MESSAGE  = 'Empty Bin Is not allowed' ).
    RETURN.
  ENDIF.


  IM_HU = |{ IM_HU ALPHA = IN } |.


  LV_HU = IM_HU.

  SELECT SINGLE SAP_HU
    FROM ZWM_EXREF
    INTO  @DATA(LV_SAP_HU)
    WHERE EXIDV EQ @IM_HU.

  IF SY-SUBRC EQ 0.
    LV_HU = LV_SAP_HU.
  ENDIF.

  SELECT SINGLE
  A~VENUM,
  A~EXIDV,
  A~SEALN5
 FROM VEKP AS A
          WHERE A~EXIDV = @LV_HU
            AND A~SEALN5 NE ''
          INTO @DATA(LS_VEKP).

  IF SY-SUBRC IS NOT INITIAL.
    EX_RETURN = VALUE #( TYPE = 'E'
                         MESSAGE = 'HU is not Valid' ).
    RETURN.
  ENDIF.


  SELECT * FROM ZSDC_HU_ART
          INTO TABLE  @DATA(LT_HU_ART)
          WHERE EXIDV = @IM_HU  .

  LOOP AT IT_DATA ASSIGNING FIELD-SYMBOL(<LFS_DATA>).

    READ TABLE LT_HU_ART ASSIGNING FIELD-SYMBOL(<LFS_HU_ART>) WITH KEY MATNR = <LFS_DATA>-MATERIAL .
    IF SY-SUBRC IS INITIAL.

      IF  (  <LFS_HU_ART>-SQTY + <LFS_DATA>-SQTY ) GT <LFS_HU_ART>-TQTY .
        EX_RETURN = VALUE #( TYPE = 'E'
                                MESSAGE = |{ 'Scan Qty is GT Req_qty for'}  { <LFS_DATA>-MATERIAL }| ) .
        RETURN.
      ELSE.
        <LFS_HU_ART>-SQTY = <LFS_HU_ART>-SQTY + <LFS_DATA>-SQTY .
      ENDIF.

    ELSE.

      LS_HU_ART-FLOOR = <LFS_DATA>-FLOOR.
      LS_HU_ART-EMPTY = IM_HU_EMPTY.
      LS_HU_ART-MATNR = <LFS_DATA>-MATERIAL.
      LS_HU_ART-EXIDV = IM_HU.
      LS_HU_ART-PQTY = <LFS_DATA>-TQTY - <LFS_DATA>-SQTY.
      LS_HU_ART-SQTY = <LFS_DATA>-SQTY.
      LS_HU_ART-TQTY = <LFS_DATA>-TQTY.
      LS_HU_ART-UDATE = SY-DATUM.
      LS_HU_ART-UNAME = IM_USER.
      LS_HU_ART-UZEIT = SY-UZEIT.
      LS_HU_ART-WERKS = IM_PLANT .
      APPEND LS_HU_ART TO LT_HU_ART .
      CLEAR LS_HU_ART.
    ENDIF.
  ENDLOOP.

  SELECT SINGLE BWART FROM MSEG INTO @DATA(LV_BWART) WHERE MBLNR = @LS_VEKP-SEALN5 .


*if LV_BWART eq '101'. ""COMMENTED BY VAIBHAV SKY  02.04.2026 09:43:47
  LT_LTAP_CREAT = VALUE #( FOR LS_DATA IN IT_DATA
                         ( MATNR    = LS_DATA-MATERIAL
                           WERKS    = IM_PLANT
                           LGORT    = '0002'
                           ANFME    = LS_DATA-SQTY
                           VLTYP    = 'V01'
                           VLPLA    =   COND #( WHEN LV_BWART EQ '101' THEN LS_VEKP-SEALN5
                                                WHEN LV_BWART EQ '311' THEN 'TRANSFER'
                                                WHEN LV_BWART EQ '305' THEN 'TRANSFER'
                                                ELSE LS_VEKP-SEALN5 )

                           NLTYP    = 'V09'
                           VLBER    = '001'
                           NLBER    = '001'
                           NLPLA    = LS_DATA-BIN
                           ALTME    = 'EA'
                           SQUIT    = 'X'
                           ) ).

  DELETE LT_LTAP_CREAT WHERE ANFME IS INITIAL.
  CALL FUNCTION 'DEQUEUE_ALL'
    EXPORTING
      _SYNCHRON = 'X'.

  """"""""""""""""""""""""""""""""""""""""""""""""""""""""Added By Vaibhav SKY  02.04.2026 10:26:57

  DATA LV_V01_QTY TYPE LQUA-VERME.

  LOOP AT IT_DATA ASSIGNING FIELD-SYMBOL(<LFS_VAL>).

    CLEAR LV_V01_QTY.

    DATA(LV_SOURCE_BIN) = COND #( WHEN LV_BWART EQ '101' THEN LS_VEKP-SEALN5
                                 WHEN LV_BWART EQ '311' THEN 'TRANSFER'
                                 WHEN LV_BWART EQ '305' THEN 'TRANSFER'
                                 ELSE LS_VEKP-SEALN5 ).


    SELECT SUM( VERME )
      FROM LQUA
      INTO @LV_V01_QTY
      WHERE LGNUM = 'SDC'
        AND LGTYP = 'V01'
        AND MATNR = @<LFS_VAL>-MATERIAL
*        AND LGPLA = @<LFS_VAL>-BIN
        AND LGPLA = @LV_SOURCE_BIN
        AND WERKS = @IM_PLANT.

    " Validation 1: V01 mein stock hi nahi hai
    IF SY-SUBRC NE 0 OR LV_V01_QTY LE 0.
      EX_RETURN = VALUE #(
        TYPE    = 'E'
        MESSAGE = |Stock not available in V01 for Material: { <LFS_VAL>-MATERIAL } Bin: { <LFS_VAL>-BIN }| ).
      RETURN.
    ENDIF.

    " Validation 2: Stock hai but SQTY (scan qty) se kam hai
    IF LV_V01_QTY LT <LFS_VAL>-SQTY.
      EX_RETURN = VALUE #(
        TYPE    = 'E'
        MESSAGE = |Insufficient stock in V01 for Material: { <LFS_VAL>-MATERIAL } | &
                  |Bin: { <LFS_VAL>-BIN } Available: { LV_V01_QTY } Required: { <LFS_VAL>-SQTY }| ).
      RETURN.
    ENDIF.

  ENDLOOP.

  """"""""""""""""""""""""""""""""""""""""""""""""""""""""Added By Vaibhav SKY  02.04.2026 10:27:10



  IF LT_LTAP_CREAT[] IS NOT INITIAL.
    CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
      EXPORTING
        I_LGNUM                = 'SDC'
        I_BWLVS                = '999'
        I_BETYP                = ' '
        I_BENUM                = ' '
        I_LZNUM                = ' '
        I_NIDRU                = ' '
        I_DRUKZ                = ' '
        I_NOSPL                = ' '
        I_UPDATE_TASK          = ' '
        I_COMMIT_WORK          = 'X'
        I_BNAME                = SY-UNAME
        I_KOMPL                = ' '
        I_SOLEX                = 0
        I_PERNR                = 0
        I_MINWM                = ' '
        I_AUSFB                = ' '
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

    IF E_TANUM IS INITIAL.
      MESSAGE
              ID SY-MSGID
            TYPE SY-MSGTY
          NUMBER SY-MSGNO
            WITH SY-MSGV1
                 SY-MSGV2
                 SY-MSGV3
                 SY-MSGV4 INTO LV_MESG.
      EX_RETURN = VALUE #( TYPE     = 'E'
                           MESSAGE  = LV_MESG ).
      RETURN.
    ELSE.
      LOOP AT LT_HU_ART ASSIGNING <LFS_HU_ART> WHERE SQTY NE 0.
        <LFS_HU_ART>-TANUM  = E_TANUM.
      ENDLOOP.

      CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING WAIT = 'X'.

      MODIFY ZSDC_HU_ART FROM TABLE LT_HU_ART.
      COMMIT WORK.
    ENDIF.
  ENDIF.

  IF IM_HU_EMPTY IS NOT INITIAL.
    REFRESH LT_LTAP_CREAT .
    SELECT DISTINCT
          _VEKP~EXIDV,
          _VEPO~WERKS,
          _VEPO~MATNR,
          _VEPO~VEMNG
                FROM VEKP AS _VEKP
                INNER JOIN VEPO AS _VEPO ON _VEPO~VENUM EQ _VEKP~VENUM
                WHERE _VEKP~EXIDV EQ @IM_HU
                  INTO TABLE @DATA(LT_EMPTY).

    LOOP AT LT_EMPTY INTO DATA(LS_EMPTY).

      READ TABLE LT_HU_ART ASSIGNING <LFS_HU_ART> WITH KEY MATNR = LS_EMPTY-MATNR.
      IF SY-SUBRC IS INITIAL .
        <LFS_HU_ART>-PQTY = <LFS_HU_ART>-TQTY - <LFS_HU_ART>-SQTY.
        LS_HU_ART-PQTY = <LFS_HU_ART>-PQTY.
        IF <LFS_HU_ART>-PQTY GT 0 .
          <LFS_HU_ART>-EMPTY = 'X'.
        ENDIF.
      ELSE.
        LS_HU_ART-PQTY = LS_EMPTY-VEMNG .

        LS_HU_ART-FLOOR = ''.
        LS_HU_ART-EMPTY = IM_HU_EMPTY.
        LS_HU_ART-MATNR = LS_EMPTY-MATNR.
        LS_HU_ART-EXIDV = IM_HU.
        LS_HU_ART-SQTY = 0.
        LS_HU_ART-TQTY = LS_HU_ART-PQTY.
        LS_HU_ART-UDATE = SY-DATUM.
        LS_HU_ART-UNAME = IM_USER.
        LS_HU_ART-UZEIT = SY-UZEIT.
        LS_HU_ART-WERKS = IM_PLANT .
        APPEND LS_HU_ART TO LT_HU_ART .

      ENDIF.

      CHECK LS_HU_ART-PQTY GT 0 .

      LS_GOODSMVT_ITEM-MOVE_TYPE    = '311'.
      LS_GOODSMVT_ITEM-MOVE_STLOC   = '0003'.
      LS_GOODSMVT_ITEM-STGE_LOC     = '0002'.
      LS_GOODSMVT_ITEM-PLANT        = IM_PLANT.
      LS_GOODSMVT_ITEM-ENTRY_QNT    = LS_HU_ART-PQTY.
      LS_GOODSMVT_ITEM-MATERIAL     = LS_EMPTY-MATNR.
      APPEND LS_GOODSMVT_ITEM TO LT_GOODSMVT_ITEM.
      CLEAR:LS_GOODSMVT_ITEM.

      LS_LTAP_CREATE-MATNR          = LS_EMPTY-MATNR.
      LS_LTAP_CREATE-WERKS          = IM_PLANT.
      LS_LTAP_CREATE-LGORT          = '0002'.
      LS_LTAP_CREATE-ANFME          = LS_HU_ART-PQTY.
      LS_LTAP_CREATE-VLTYP          = 'V01'.
      LS_LTAP_CREATE-VLPLA          = COND #( WHEN LV_BWART EQ '101' THEN LS_VEKP-SEALN5
                                              WHEN LV_BWART EQ '311' THEN 'TRANSFER'
                                              WHEN LV_BWART EQ '305' THEN 'TRANSFER' ELSE LS_VEKP-SEALN5 ).
      LS_LTAP_CREATE-NLTYP          = 'V04'.
      LS_LTAP_CREATE-NLPLA          = 'IN-TRANSIT'.
      LS_LTAP_CREATE-ALTME          = 'EA'.
      LS_LTAP_CREATE-SQUIT          = 'X'.
      APPEND LS_LTAP_CREATE TO LT_LTAP_CREAT.
      CLEAR LS_LTAP_CREATE.

      AT END OF EXIDV.

        BREAK SAP_ABAP.
        DELETE LT_LTAP_CREAT WHERE ANFME IS INITIAL.
        DELETE LT_GOODSMVT_ITEM WHERE ENTRY_QNT IS INITIAL.
        CLEAR:GOODSMVT_HEADER,GOODSMVT_CODE,GOODSMVT_HEADRET,MATERIALDOCUMENT,MATDOCUMENTYEAR.

        GOODSMVT_HEADER-HEADER_TXT = |GRC-{ LS_VEKP-EXIDV }|.
        GOODSMVT_HEADER-DOC_DATE = SY-DATUM.
        GOODSMVT_HEADER-PSTNG_DATE = SY-DATUM.
        GOODSMVT_CODE-GM_CODE = '04'.
        IF LT_GOODSMVT_ITEM IS NOT INITIAL.
          CALL FUNCTION 'BAPI_GOODSMVT_CREATE'
            EXPORTING
              GOODSMVT_HEADER  = GOODSMVT_HEADER
              GOODSMVT_CODE    = GOODSMVT_CODE
            IMPORTING
              GOODSMVT_HEADRET = GOODSMVT_HEADRET
              MATERIALDOCUMENT = MATERIALDOCUMENT
              MATDOCUMENTYEAR  = MATDOCUMENTYEAR
            TABLES
              GOODSMVT_ITEM    = LT_GOODSMVT_ITEM
              RETURN           = RETURN.
        ENDIF.
        IF MATERIALDOCUMENT IS INITIAL.
          CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
          READ TABLE RETURN INTO DATA(LS_RETURN) WITH KEY TYPE = 'E'.
          EX_RETURN = VALUE #(
                               TYPE    = 'E'
                               MESSAGE = LS_RETURN-MESSAGE ).
          RETURN.
        ELSE.

          LOOP AT  LT_HU_ART  ASSIGNING <LFS_HU_ART> WHERE EMPTY = 'X'.
            <LFS_HU_ART>-MBLNR = MATERIALDOCUMENT .
          ENDLOOP.
          MODIFY ZSDC_HU_ART FROM TABLE LT_HU_ART.

          CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING WAIT = 'X'.
          REFRESH: LT_GOODSMVT_ITEM,RETURN.
          CLEAR:E_TANUM.
          WAIT UP TO 1 SECONDS.
          CALL FUNCTION 'DEQUEUE_ALL'
            EXPORTING
              _SYNCHRON = 'X'.

          IF LT_LTAP_CREAT IS NOT INITIAL.
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

          ENDIF.
          REFRESH LT_LTAP_CREAT.

          IF E_TANUM IS NOT INITIAL.

            CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING WAIT = 'X'.


            UPDATE ZSDC_HU_ART SET TANUM      =   E_TANUM
                                   WHERE EXIDV = LS_VEKP-EXIDV.


          ELSE.
            MESSAGE
                    ID SY-MSGID
                  TYPE SY-MSGTY
                NUMBER SY-MSGNO
                  WITH SY-MSGV1
                       SY-MSGV2
                       SY-MSGV3
                       SY-MSGV4 INTO LV_MESG.
            CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
            EX_RETURN = VALUE #( TYPE     = 'E'
                                 MESSAGE  = LV_MESG ).
            RETURN.
          ENDIF.
        ENDIF.
      ENDAT.
    ENDLOOP.
  ENDIF.
ENDFUNCTION.