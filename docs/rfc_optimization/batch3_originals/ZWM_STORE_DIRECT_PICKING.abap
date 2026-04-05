FUNCTION ZWM_STORE_DIRECT_PICKING.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"     VALUE(EX_MBLNR) TYPE  MBLNR
*"     VALUE(EX_MJAHR) TYPE  MJAHR
*"  TABLES
*"      IT_DATA TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------
  DATA: LT_DATA  TYPE TY_T_DATA,
        LR_DATA  TYPE REF TO ZWM_STORE_STRU,
        LR_DATA2 TYPE REF TO TY_DATA.

  DATA: L_MATNR    TYPE MATNR,
        L_EXIDV    TYPE EXIDV,
        L_TABIX    TYPE SYTABIX,
        L_NUMBER   TYPE TANUM,
        L_DOC_TYPE TYPE CHAR1,
        L_RC       TYPE SYSUBRC.

  DATA: LT_LTAP_CREATE TYPE STANDARD TABLE OF LTAP_CREAT INITIAL SIZE 0,
        LT_LTAP        TYPE STANDARD TABLE OF LTAP_VB    INITIAL SIZE 0,
        LT_VEKP        TYPE STANDARD TABLE OF VEKP       INITIAL SIZE 0.

  DATA: LS_LTAP_CREATE TYPE LTAP_CREAT,
        LS_ST_ACTIVE   TYPE ZWM_ST_ACTIVE,
        LS_VEKP        TYPE VEKP,
        LS_DATA        TYPE ZWM_STORE_STRU,
        LS_PICK        TYPE ZST_PICK,
        L_TANUM        TYPE TANUM.

  DATA: LS_HUSDC TYPE ZMM_HUSDC,
        LT_HUSDC TYPE STANDARD TABLE OF ZMM_HUSDC INITIAL SIZE 0.

  DATA: LV_COUNT TYPE MBLPO,
        L_VGBEL  TYPE VGBEL,
        LV_LGORT TYPE LGORT_D,
        L_MBLNR  TYPE MBLNR,
        L_MJAHR  TYPE MJAHR,
        L_TANUM2 TYPE TANUM,
        L_USER   TYPE WWWOBJID.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_USER
    IMPORTING
      OUTPUT = L_USER.

  TRANSLATE L_USER TO UPPER CASE.
  READ TABLE IT_DATA INTO LS_DATA INDEX 1.

  IF LS_DATA-PICNR IS INITIAL.
    SELECT SINGLE *
      FROM ZWM_ST_ACTIVE
      INTO LS_ST_ACTIVE
      WHERE LGNUM = IM_LGNUM
        AND WERKS = IM_WERKS.

    IF SY-SUBRC IS INITIAL.
      IF LS_ST_ACTIVE-PICKING IS INITIAL.
        EX_RETURN-TYPE = C_ERROR.
        CONCATENATE 'Direct Picking for site' IM_WERKS 'not allowed !!' INTO EX_RETURN-MESSAGE SEPARATED BY SPACE.
        RETURN.
      ENDIF.
    ELSE.
      EX_RETURN-TYPE = C_ERROR.
      CONCATENATE 'entry missing in ZSDC_ST_ALLOW for ' IM_WERKS INTO EX_RETURN-MESSAGE SEPARATED BY SPACE.
      RETURN.
    ENDIF.
  ENDIF.

*  IF ex_return-message IS INITIAL.
  LOOP AT IT_DATA REFERENCE INTO LR_DATA.
    TRANSLATE LR_DATA->BIN TO UPPER CASE.

*Convert into internal format "VKS-11.12.2021
*      CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
*        EXPORTING
*          input  = lr_data->material
*        IMPORTING
*          output = l_matnr.

    L_MATNR = LR_DATA->MATERIAL.
    READ TABLE LT_DATA REFERENCE INTO LR_DATA2 WITH KEY MATNR = L_MATNR LGPLA = LR_DATA->BIN.
    IF SY-SUBRC IS NOT INITIAL.
      APPEND INITIAL LINE TO LT_DATA REFERENCE INTO LR_DATA2.

      CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
        EXPORTING
          INPUT  = LR_DATA->PICNR
        IMPORTING
          OUTPUT = LR_DATA->PICNR.

      MOVE: L_MATNR        TO LR_DATA2->MATNR,
            IM_WERKS+1(3)  TO  LR_DATA2->LGTYP,
            LR_DATA->BIN   TO LR_DATA2->LGPLA,
            LR_DATA->PICNR TO LR_DATA2->PICNR.
    ENDIF.
    LR_DATA2->MENGE = LR_DATA2->MENGE + LR_DATA->SCAN_QTY.

  ENDLOOP.
*  IF SY-SYSID NE 'S4P'.
  IF LR_DATA->PICNR IS NOT INITIAL.
    LOOP AT LT_DATA REFERENCE INTO LR_DATA2.

      SELECT SINGLE * FROM ZWM_STORE_HST INTO @DATA(LS_HST1) WHERE DOC_TYPE = '3'
            AND PICNR = @LR_DATA2->PICNR
            AND VLPLA = @LR_DATA2->LGPLA
            AND WERKS = @IM_WERKS
            AND MATNR = @LR_DATA2->MATNR
            AND ANFME = @LR_DATA2->MENGE
            AND SLOC =  '0002'.
      IF SY-SUBRC IS INITIAL   .
        LR_DATA2->MATNR = ''.
      ENDIF.

    ENDLOOP.
    DELETE LT_DATA WHERE MATNR IS INITIAL .

  ENDIF.

*  ENDIF.
*Set location
  DATA: LS_STORE TYPE ZWMS_STORE_0008.

  SELECT SINGLE *
    FROM ZWMS_STORE_0008
    INTO LS_STORE
    WHERE WERKS = IM_WERKS.

  IF LS_STORE-ACTIVE IS INITIAL.
    LV_LGORT = '0001'.
  ELSE.
    LV_LGORT = '0002'.
  ENDIF.

  L_DOC_TYPE = '3'.


""commented on 04-sep-25 by jitendra as discussed with nishant
**  IF IM_WERKS = 'HD22'. "VKS-19.03.2021
**    LV_LGORT = '0008'.
**  ENDIF.


  IF LT_DATA[] IS NOT INITIAL.

    PERFORM F_SAVE_TEMP_DATA_PICKING USING LT_DATA
                                           IM_WERKS
                                           L_USER
                                           '0002'
                                           LV_LGORT
                                           L_NUMBER
                                           L_DOC_TYPE.

    PERFORM F_TRANSFER_STOCK USING IM_LGNUM
                                   IM_WERKS
                                   '0002'
                                   LV_LGORT
                                   LT_DATA
                                   L_NUMBER
                                   L_DOC_TYPE
                          CHANGING EX_MBLNR
                                   EX_MJAHR
                                   EX_TANUM
                                   EX_RETURN.

    PERFORM F_CLEAR_V04_FROM_MSA_BIN USING  IM_LGNUM
                                            IM_WERKS
                                            '0002'
                                            LV_LGORT
                                            LT_DATA
                                            L_NUMBER
                                            L_DOC_TYPE
                                   CHANGING EX_MBLNR
                                            EX_MJAHR
                                            EX_TANUM
                                            EX_RETURN.
*  ENDIF.

*Movement 0008-0001 for DH24 "VKS-19.03.2021
    IF IM_WERKS = 'HD22'.
      DATA: LS_MEAN     TYPE MEAN,
            EX_MARD     TYPE MARD,
            ET_EAN_DATA TYPE TABLE OF MARM.

      LOOP AT IT_DATA INTO LS_DATA.
        SELECT SINGLE * FROM MEAN INTO LS_MEAN WHERE MATNR = LS_DATA-MATERIAL AND HPEAN = 'X'.

        CALL FUNCTION 'ZWM_STORE_GET_STOCK'
          EXPORTING
            IM_WERKS      = IM_WERKS
            IM_LGORT      = '0001'
            IM_EAN11      = LS_MEAN-EAN11
            IM_STOCK_TAKE = ''
          IMPORTING
            EX_RETURN     = EX_RETURN
            EX_MARD       = EX_MARD
          TABLES
            ET_EAN_DATA   = ET_EAN_DATA.

        REFRESH: ET_EAN_DATA.
        CLEAR: LS_DATA,LS_MEAN,EX_MARD.
      ENDLOOP.

      CALL FUNCTION 'ZWM_STORE_TRANSFER_SLOC_TO_SLO'
        EXPORTING
          IM_WERKS      = IM_WERKS
          IM_LGORT_SRC  = '0002' "ADDED 0002 ON 04-SEP-25 BY JITENDRA
          IM_LGORT_DEST = '0001'
          IM_USER       = IM_USER
          IM_LGNUM      = 'SDC'
        IMPORTING
          EX_RETURN     = EX_RETURN
          EX_TANUM      = EX_TANUM
          EX_MBLNR      = EX_MBLNR
          EX_MJAHR      = EX_MJAHR
        TABLES
          IT_DATA       = IT_DATA.
    ENDIF.


  ELSE.
* IF sy-subrc IS NOT INITIAL.
    EX_RETURN-MESSAGE = 'Data not Found '.
    EX_RETURN-TYPE = 'E'.
    RETURN.
*    ENDIF.

  ENDIF.

ENDFUNCTION.