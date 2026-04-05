FUNCTION ZWM_STORE_GET_HUS.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_VBELN) TYPE  VBELN_VF
*"     VALUE(IM_EDOCNO) TYPE  ZDOCNO2
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_HUS TYPE  ZWM_STORE_STRU_T OPTIONAL
*"----------------------------------------------------------------------
  DATA: L_INVNO   TYPE VBELN_VF,
        LV_TABIX  TYPE SY-TABIX,
        LS_ZGENHD TYPE ZGENHD,
        LT_ZGENHD TYPE TABLE OF ZGENHD.

  DATA: LS_VBRK  TYPE VBRK,
        LS_ITEMS TYPE VEPOVB,
        LS_EXREF TYPE ZWM_EXREF,
        LT_ITEMS TYPE HUM_HU_ITEM_T,
        LT_HST   TYPE STANDARD TABLE OF ZWM_STORE_HST,
        LS_LIKP  TYPE LIKP,
        LS_VBFA  TYPE VBFA,
        LS_VBFA1 TYPE VBFA,
        LT_LIKP  TYPE STANDARD TABLE OF LIKP,
        LT_VBFA  TYPE STANDARD TABLE OF VBFA,
        LT_EXREF TYPE STANDARD TABLE OF ZWM_EXREF,
        LT_VBFA1 TYPE STANDARD TABLE OF VBFA.

  FIELD-SYMBOLS: <LFS_HUS> TYPE ZWM_STORE_STRU.


*    ex_return-TYPE = c_error .
*    ex_return-MESSAGE = 'Use SAP T-code YWM_STORE/ZVLMOVE4'.
*    RETURN.


  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_VBELN
    IMPORTING
      OUTPUT = L_INVNO.

  SELECT SINGLE * FROM VBRK
            INTO LS_VBRK
            WHERE VBELN = L_INVNO.
  IF SY-SUBRC IS NOT INITIAL.
    EX_RETURN-TYPE = C_ERROR .
    EX_RETURN-MESSAGE = 'Invalid Invoice'.
    RETURN.
  ENDIF.


  SELECT SINGLE WERKS FROM VBRP INTO @DATA(LV_WERKS) WHERE VBELN = @L_INVNO.

*Gate Entry validation against Invoice - VKS-12.02.2022
  SELECT SINGLE * FROM ZGENHD
    INTO LS_ZGENHD
    WHERE INVNO  = L_INVNO
       OR INVNO1 = L_INVNO
       OR INVNO2 = L_INVNO
       OR INVNO3 = L_INVNO
       OR INVNO4 = L_INVNO
       OR INVNO5 = L_INVNO.
  IF SY-SUBRC <> 0.
    EX_RETURN-TYPE = C_ERROR .
    EX_RETURN-MESSAGE = 'Gate Entry Pending against Invoice'.
    RETURN.
  ENDIF.

  SELECT * FROM VBFA
          INTO TABLE LT_VBFA
          WHERE VBELN =  L_INVNO
            AND VBTYP_N IN ('M','U','5').

  SORT LT_VBFA BY VBELV.
  DELETE ADJACENT DUPLICATES FROM LT_VBFA COMPARING VBELV.

  IF LT_VBFA[] IS NOT INITIAL.
    SELECT * FROM LIKP INTO TABLE LT_LIKP
                    FOR ALL ENTRIES IN LT_VBFA
                    WHERE VBELN = LT_VBFA-VBELV.

    IF LT_LIKP[] IS NOT INITIAL.
      SORT LT_LIKP BY VBELN.
      SELECT * FROM VBFA INTO TABLE LT_VBFA1
                       FOR ALL ENTRIES IN LT_LIKP
                         WHERE VBELV = LT_LIKP-VBELN
                           AND VBTYP_N = 'X'.
    ENDIF.

    SORT LT_VBFA1 BY VBELV.
    LOOP AT LT_VBFA INTO LS_VBFA .
      READ TABLE LT_LIKP INTO LS_LIKP WITH KEY VBELN = LS_VBFA-VBELV
                                              BINARY SEARCH .
      IF SY-SUBRC IS INITIAL .
        IF   LS_LIKP-KUNNR EQ IM_WERKS.
          CLEAR LV_TABIX .
          READ TABLE LT_VBFA1 WITH KEY VBELV = LS_LIKP-VBELN
                                        BINARY SEARCH
                                        TRANSPORTING NO FIELDS .
          IF SY-SUBRC IS INITIAL .
            LV_TABIX = SY-TABIX .

            LOOP AT LT_VBFA1 INTO LS_VBFA1 FROM LV_TABIX.
              IF LS_VBFA1-VBELV NE LS_LIKP-VBELN.
                EXIT.
              ENDIF.

              LS_ITEMS-VENUM = LS_VBFA1-VBELN.
              APPEND LS_ITEMS TO LT_ITEMS.
              CLEAR LS_ITEMS.
              CLEAR LS_VBFA1.
            ENDLOOP.
          ENDIF.
        ELSE.
          EX_RETURN-TYPE = C_ERROR.
          EX_RETURN-MESSAGE = 'Invoice is not for this store'.
          EXIT.
        ENDIF.
      ENDIF.
      CLEAR LS_LIKP.
      CLEAR LS_VBFA.
    ENDLOOP.
    BREAK-POINT ID Z_V2CHECK.
    IF LT_ITEMS[] IS NOT INITIAL.
      SELECT EXIDV AS HU_NO
        FROM VEKP
        INTO CORRESPONDING FIELDS OF TABLE ET_HUS
        FOR ALL ENTRIES IN LT_ITEMS
        WHERE VENUM  = LT_ITEMS-VENUM
          AND ( STATUS = '0050' OR STATUS = '0040' )
          AND SEALN5 = SPACE.

      IF SY-SUBRC IS NOT INITIAL.
        EX_RETURN-TYPE = C_ERROR.
        EX_RETURN-MESSAGE = 'No Data to process'.
        RETURN.
      ELSE.

        SELECT * FROM ZWM_STORE_HST
                  INTO TABLE LT_HST
                  FOR ALL ENTRIES IN ET_HUS
                    WHERE EXIDV = ET_HUS-HU_NO.
        IF SY-SUBRC = 0.
          SORT LT_HST BY EXIDV.
          DELETE LT_HST WHERE WERKS = LV_WERKS.
        ENDIF.
      ENDIF.

      SELECT * FROM ZWM_EXREF
            INTO TABLE LT_EXREF
            FOR ALL ENTRIES IN ET_HUS
            WHERE SAP_HU = ET_HUS-HU_NO.

      IF SY-SUBRC IS INITIAL .
        SORT LT_EXREF BY SAP_HU.
      ENDIF.

      LOOP AT ET_HUS ASSIGNING <LFS_HUS>.
        READ TABLE LT_HST WITH KEY EXIDV = <LFS_HUS>-HU_NO
                                    BINARY SEARCH
                                    TRANSPORTING NO FIELDS .
        IF SY-SUBRC IS INITIAL.
          <LFS_HUS>-HU_NO = ''.
        ELSE.

          READ TABLE LT_EXREF INTO LS_EXREF WITH KEY SAP_HU = <LFS_HUS>-HU_NO
                                                  BINARY SEARCH.
          IF SY-SUBRC IS INITIAL.
            <LFS_HUS>-HU_NO = LS_EXREF-EXIDV.
          ENDIF.

          CALL FUNCTION 'CONVERSION_EXIT_ALPHA_OUTPUT'
            EXPORTING
              INPUT  = <LFS_HUS>-HU_NO
            IMPORTING
              OUTPUT = <LFS_HUS>-HU_NO.
        ENDIF.

        CLEAR LS_EXREF.
      ENDLOOP.
      DELETE ET_HUS WHERE HU_NO IS INITIAL .
      IF ET_HUS[] IS INITIAL.
        EX_RETURN-TYPE = C_ERROR.
        EX_RETURN-MESSAGE = 'No HU Pending'.
      ENDIF.
    ENDIF.
  ELSE.
    EX_RETURN-TYPE = C_ERROR.
    EX_RETURN-MESSAGE = 'Invalid Invoice'.
  ENDIF.
ENDFUNCTION.