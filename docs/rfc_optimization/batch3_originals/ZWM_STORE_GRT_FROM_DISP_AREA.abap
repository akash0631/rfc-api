FUNCTION ZWM_STORE_GRT_FROM_DISP_AREA.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGORT_SRC) TYPE  LGORT_D OPTIONAL
*"     VALUE(IM_LGORT_DEST) TYPE  LGORT_D DEFAULT '0005'
*"     VALUE(IM_WERKS_DES) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_PACK_MAT) TYPE  MATNR OPTIONAL
*"     VALUE(IM_CATEGORY) TYPE  TEXTLINE OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"     VALUE(EX_MBLNR) TYPE  MBLNR
*"     VALUE(EX_MJAHR) TYPE  MJAHR
*"     VALUE(EX_EXIDV) TYPE  EXIDV
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------
  BREAK-POINT ID Z_V2CHECK.
  DATA:
    LT_DATA  TYPE TY_T_DATA,
    LR_DATA  TYPE REF TO ZWM_STORE_STRU,
    GT_DATA  TYPE STANDARD TABLE OF ZWM_STORE_STRU,
    LR_DATA2 TYPE REF TO TY_DATA.

  DATA:
    L_MATNR    TYPE MATNR,
    L_EXIDV    TYPE EXIDV,
    L_TABIX    TYPE SYTABIX,
    L_NUMBER   TYPE NUMC10,
    L_DOC_TYPE TYPE CHAR1,
    L_RC       TYPE SYSUBRC.

  DATA:
    LT_HU  TYPE HUM_HU_ITEM_T,
    LT_HDR TYPE HUM_HU_HEADER_T.
  DATA:
      LR_HU     TYPE REF TO VEPOVB.

  DATA:
    LT_LTAP_CREATE TYPE STANDARD TABLE OF LTAP_CREAT      INITIAL SIZE 0,
    LT_LTAP        TYPE STANDARD TABLE OF LTAP_VB         INITIAL SIZE 0,
    LT_VEKP        TYPE STANDARD TABLE OF VEKP            INITIAL SIZE 0.

  DATA:
    LS_LTAP_CREATE TYPE LTAP_CREAT,
    LS_VEKP        TYPE VEKP,
    L_TANUM        TYPE TANUM.

  DATA:
    LS_HUSDC TYPE ZMM_HUSDC,
    LT_HUSDC TYPE STANDARD TABLE OF ZMM_HUSDC INITIAL SIZE 0.

  DATA:
      LV_COUNT TYPE MBLPO.

  DATA:
      L_VGBEL  TYPE VGBEL.

  DATA:
    L_MBLNR  TYPE MBLNR,
    L_MJAHR  TYPE MJAHR,
    L_TANUM2 TYPE TANUM,
    L_USER   TYPE WWWOBJID.

  DATA:
      L_PACK_MAT TYPE MATNR.

  SELECT SINGLE HUB FROM ZWMHUB01 INTO @IM_WERKS_DES WHERE PLANT = @IM_WERKS.
  BREAK-POINT ID Z_V2CHECK.
  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_PACK_MAT
    IMPORTING
      OUTPUT = L_PACK_MAT.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_USER
    IMPORTING
      OUTPUT = L_USER.

  GT_DATA = IT_DATA[].

  LOOP AT IT_DATA REFERENCE INTO LR_DATA.

* Convert into internal format
    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        INPUT  = LR_DATA->MATERIAL
      IMPORTING
        OUTPUT = L_MATNR.

    READ TABLE LT_DATA REFERENCE INTO LR_DATA2
    WITH KEY MATNR = L_MATNR
             LGPLA = LR_DATA->BIN.

    IF SY-SUBRC IS NOT INITIAL.
      APPEND INITIAL LINE TO LT_DATA REFERENCE INTO LR_DATA2.
      MOVE:
        L_MATNR        TO LR_DATA2->MATNR,
        LR_DATA->BIN   TO LR_DATA2->LGPLA.
    ENDIF.

    LR_DATA2->MENGE = LR_DATA2->MENGE + LR_DATA->SCAN_QTY.

  ENDLOOP.
  IM_LGORT_DEST = '0005'. "added by jitendra as per anshu and puru sir 05-feb-26
  L_DOC_TYPE = 'G'.

  ""27.02.2026 09:53:25 Start Added By Priya .



  DATA: LV_TIME_LOW  TYPE SY-UZEIT,
        LV_TIME_HIGH TYPE SY-UZEIT.

  LV_TIME_HIGH = SY-UZEIT.
  LV_TIME_LOW  = SY-UZEIT - 300.

  SELECT COUNT( * ) FROM @GT_DATA AS A INTO @DATA(LV_COUNT_DATA).

  SELECT COUNT( * ) FROM ZWM_STORE_HST AS _HST
            FOR ALL ENTRIES IN @IT_DATA WHERE
            _HST~MATNR EQ      @IT_DATA-MATERIAL
            AND _HST~WERKS  EQ @IM_WERKS
            AND _HST~SLOC   EQ @IM_LGORT_SRC
            AND _HST~ANFME  EQ @IT_DATA-SCAN_QTY
            AND _HST~DLOC   EQ @IM_LGORT_DEST
            AND _HST~ERDAT  EQ @SY-DATUM
            AND _HST~ERZET  BETWEEN @LV_TIME_LOW AND @LV_TIME_HIGH
            INTO @DATA(LV_COUNT_TAB).
  IF SY-SUBRC IS INITIAL.
    IF LV_COUNT_DATA EQ LV_COUNT_TAB.
      DO 10 TIMES.
        SELECT SINGLE _HST~EXIDV FROM ZWM_STORE_HST AS _HST
                                 INNER JOIN @GT_DATA AS _DATA
                                 ON _HST~MATNR EQ _DATA~MATERIAL
                                 AND _HST~ANFME EQ _DATA~SCAN_QTY
                                 AND _HST~WERKS EQ @IM_WERKS
                                 AND _HST~DLOC EQ  @IM_LGORT_DEST
                                 AND _HST~ERDAT EQ @SY-DATUM
                                 AND _HST~ERZET BETWEEN @LV_TIME_LOW AND @LV_TIME_HIGH
                                 INTO @DATA(LV_HU).
        IF LV_HU IS NOT INITIAL .
          EX_RETURN = VALUE #( TYPE     = 'I'
                             MESSAGE      = | 'HU'  { LV_HU } 'Created' | ).
          RETURN.
        ENDIF.
      ENDDO.
    ENDIF.
    IF LV_COUNT_DATA EQ LV_COUNT_TAB.
      EX_RETURN   = VALUE #( TYPE         = 'E'
                         MESSAGE      = 'Combination of data is already being processed' ).
      RETURN.
    ENDIF.
  ENDIF.

  ""% End Added By Priya.""

  PERFORM F_SAVE_TEMP_DATA_ST_TAKE USING
                                   LT_DATA
                                   IM_LGORT_SRC "'0001' ""ADDED BY JITENDRA SINGH @SKYPER-NOIDA 18.09.2025 15:27:19
                                   '0005'
                                   L_NUMBER
                                   IM_WERKS
                                   L_USER
                                   L_DOC_TYPE.

  PERFORM F_TRANSFER_STOCK USING IM_LGNUM
                                 IM_WERKS
                                 IM_LGORT_SRC
                                 IM_LGORT_DEST
                                 LT_DATA
                                 L_NUMBER
                                 L_DOC_TYPE
                        CHANGING EX_MBLNR
                                 EX_MJAHR
                                 EX_TANUM
                                 EX_RETURN.

  CHECK EX_RETURN-TYPE = C_SUCCESS .
  PERFORM F_CREATE_TO USING IM_LGNUM
                            IM_WERKS
                            IM_WERKS_DES
                            L_PACK_MAT
                            L_NUMBER
                            L_DOC_TYPE
                            '0005'
                            LT_DATA
                   CHANGING EX_EXIDV
                            EX_RETURN.

  IF EX_EXIDV IS NOT INITIAL.
    UPDATE VEKP
       SET EPC1  = IM_CATEGORY
     WHERE EXIDV = EX_EXIDV.
  ENDIF.


ENDFUNCTION.