FUNCTION ZWM_STORE_GRT_FROM_MSA.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_WERKS_DES) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_PACK_MAT) TYPE  MATNR
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
  DATA:
    LT_DATA  TYPE TY_T_DATA,
    LR_DATA  TYPE REF TO ZWM_STORE_STRU,
    GT_DATA  TYPE STANDARD TABLE OF ZWM_STORE_STRU,    ""28.02.2026 09:55:49""Added by Priya Skyper""
    LR_DATA2 TYPE REF TO TY_DATA.

  DATA:
    L_MATNR    TYPE MATNR,
    L_EXIDV    TYPE EXIDV,
    L_TABIX    TYPE SYTABIX,
    L_NUMBER   TYPE TANUM,
    L_DOC_TYPE TYPE CHAR1,
    L_RC       TYPE SYSUBRC.

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

  DATA: L_PACK_MAT TYPE MATNR.

*  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT' "VKS-11.12.2021
*    EXPORTING
*      input  = im_pack_mat
*    IMPORTING
*      output = l_pack_mat.
  L_PACK_MAT = IM_PACK_MAT.
  SELECT SINGLE HUB FROM ZWMHUB01 INTO @IM_WERKS_DES WHERE PLANT = @IM_WERKS.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_USER
    IMPORTING
      OUTPUT = L_USER.

   GT_DATA = IT_DATA[].

  LOOP AT IT_DATA REFERENCE INTO LR_DATA.
    TRANSLATE LR_DATA->BIN TO UPPER CASE.
* Convert into internal format
*    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT' "VKS-11.12.2021
*      EXPORTING
*        input  = lr_data->material
*      IMPORTING
*        output = l_matnr.
    L_MATNR = LR_DATA->MATERIAL.

    READ TABLE LT_DATA REFERENCE INTO LR_DATA2
    WITH KEY MATNR = L_MATNR
             LGPLA = LR_DATA->BIN.

    IF SY-SUBRC IS NOT INITIAL.
      APPEND INITIAL LINE TO LT_DATA REFERENCE INTO LR_DATA2.
      MOVE:
        L_MATNR        TO LR_DATA2->MATNR,
        IM_WERKS+1(3)  TO  LR_DATA2->LGTYP,
        LR_DATA->BIN TO LR_DATA2->LGPLA.
    ENDIF.

    LR_DATA2->MENGE = LR_DATA2->MENGE + LR_DATA->SCAN_QTY.

  ENDLOOP.
  L_DOC_TYPE = 'G'.


  ""28.02.2026 09:54:18 Start Added By Priya .

  DATA: LV_TIME_LOW  TYPE SY-UZEIT,
        LV_TIME_HIGH TYPE SY-UZEIT.

  LV_TIME_HIGH = SY-UZEIT.
  LV_TIME_LOW  = SY-UZEIT - 300.

  SELECT COUNT( * ) FROM @GT_DATA AS A INTO @DATA(LV_COUNT_DATA).

  SELECT COUNT( * ) FROM ZWM_STORE_HST AS _HST
            FOR ALL ENTRIES IN @IT_DATA WHERE
            _HST~MATNR      EQ      @IT_DATA-MATERIAL
            AND _HST~WERKS  EQ @IM_WERKS
*            AND _HST~SLOC   EQ @IT_DATA-STOR_LOC
            AND _HST~ANFME  EQ @IT_DATA-SCAN_QTY
*            AND _HST~DLOC   EQ @IM_LGORT_DEST
            AND _HST~VLPLA  EQ  @IT_DATA-BIN
            AND _HST~ERDAT  EQ @SY-DATUM
            AND _HST~ERZET  BETWEEN @LV_TIME_LOW AND @LV_TIME_HIGH
            INTO @DATA(LV_COUNT_TAB).
  IF SY-SUBRC IS INITIAL.
    IF LV_COUNT_DATA EQ LV_COUNT_TAB.
      DO 10 TIMES.
        SELECT SINGLE _HST~EXIDV FROM ZWM_STORE_HST AS _HST
                                 INNER JOIN @GT_DATA AS _DATA
                                 ON _HST~MATNR     EQ _DATA~MATERIAL
                                 AND _HST~ANFME    EQ _DATA~SCAN_QTY
                                  AND _HST~VLPLA   EQ  _DATA~BIN
                                 AND _HST~WERKS    EQ @IM_WERKS
*                                 AND _HST~DLOC EQ  @IM_LGORT_DEST
                                 AND _HST~ERDAT    EQ @SY-DATUM
                                 AND _HST~ERZET BETWEEN @LV_TIME_LOW AND @LV_TIME_HIGH
                                 INTO @DATA(LV_HU).
        IF LV_HU IS NOT INITIAL .
          EX_RETURN = VALUE #( TYPE     = 'I'
                             MESSAGE      = | 'HU'  { LV_HU } 'Created already' | ).
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
                                   '0002'
                                   '0005'
                                   L_NUMBER
                                   IM_WERKS
                                   L_USER
                                   L_DOC_TYPE.

  PERFORM F_TRANSFER_STOCK USING IM_LGNUM
                                 IM_WERKS
                                 '0002'
                                 '0005'
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