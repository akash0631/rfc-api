FUNCTION ZWM_CREATE_HU_AND_ASSIGN.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'V2R'
*"     VALUE(IM_VBELN) TYPE  VBELN_VL OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_EXIDV) TYPE  EXIDV OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA STRUCTURE  VERPO OPTIONAL
*"      IT_BIN_EMPTY STRUCTURE  ZWM_PLESS_BIN OPTIONAL
*"----------------------------------------------------------------------
  CLEAR: EX_RETURN,G_SYS,G_COMP.


  DATA: L_EXIDV            TYPE EXIDV,
        LV_EXIDV           TYPE EXIDV,
        LV_WERKS           TYPE WERKS_D,
        L_USER             TYPE WWWOBJID,
        LS_ZWM_HU_LOG      TYPE ZWM_HU_LOG,
        L_TOT_QTY          TYPE TMENG,
        L_TOT_QTY_S        TYPE STRING,
        LT_DATA            TYPE TAB_VERPO,
        LS_HEADER_PROPOSAL TYPE HUHDR_PROPOSAL,
        LS_HEADER          TYPE VEKPVB,
        LV_POSNR           TYPE POSNR,
        LS_HU_BIN          TYPE ZWM_HU_BIN,
        LS_VERPO           LIKE LINE OF IT_DATA,
        LS_HHT_DATA        TYPE VERPO,
        LS_PICKLIST        TYPE ZADVERB_PICKLIST,
        LS_HUHEADER        TYPE BAPIHUHEADER,
        LT_RETURN          TYPE BAPIRET2_T,
        LT_HU_BIN          TYPE STANDARD TABLE OF ZWM_HU_BIN,
        LR_DATA            TYPE REF TO VERPO,
        LR_RETURN          TYPE REF TO BAPIRET2,
        LV_EXIDV1          TYPE ZSAPHU.

  DATA: L_MATNR      TYPE MATNR,
        L_VBELN      TYPE VBELN_VL,
        L_TMENG      TYPE TMENG,
        LS_VBKOK_WA  TYPE VBKOK,
        LS_PROTT     TYPE PROTT,
        LF_ERROR_GI  TYPE XFELD,
        L_TIME_STAMP TYPE TIMESTAMPL,
        L_BKG_ERROR  TYPE CHAR1,
        LS_TID       TYPE ARFCTID,
        LT_ERRORS    TYPE TABLE OF ARFCERRORS,
        LT_PROT      TYPE TABLE OF PROTT,
        LS_TMP       TYPE VERPO.

*Paperless Picking X mark BINS
  DATA: GT_BIN_EMPTY     TYPE TABLE OF ZWM_PLESS_BIN,
        GT_BIN_EMPTY_SUM TYPE TABLE OF ZWM_PLESS_BIN,
        GW_BIN_EMPTY_SUM TYPE ZWM_PLESS_BIN,
        GW_BIN_EMPTY     TYPE ZWM_PLESS_BIN,
        WA_BIN_EMPTY     TYPE ZWM_PLESS_BIN,
        GT_DATA          TYPE TABLE OF VERPO,
        IT_PICKLIST      TYPE ZADVERB_PICKLIST_TT,
        GT_DATA_SUM      TYPE TABLE OF VERPO,
        GW_DATA          TYPE VERPO,
        GW_DATA_SUM      TYPE VERPO.


*Update HU Backup data - VKS-22.01.2021
  DATA: LS_DATA  TYPE ZBACKUPHU_DATA,
        LT_VEPO1 TYPE TABLE OF VEPO,
        LS_VEPO1 TYPE VEPO,
        LS_VEKP1 TYPE VEKP,
*        LS_HU_BIN TYPE ZWM_HU_BIN,
        LV_HUQTY TYPE VEMNG,
        LS_EXREF TYPE ZWM_EXREF.
*  GT_BIN_EMPTY = IT_BIN_EMPTY[].

  DATA: GV_GRS_WT TYPE BRGEW.  " ++BHARAT ON 04.08.2022

*Convert into internal format
  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_VBELN
    IMPORTING
      OUTPUT = L_VBELN.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_USER
    IMPORTING
      OUTPUT = L_USER.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_EXIDV
    IMPORTING
      OUTPUT = LV_EXIDV.

  GT_DATA = IT_DATA[].

  SORT IT_BIN_EMPTY[] BY VBELN VLPLA.
*  DELETE ADJACENT DUPLICATES FROM IT_BIN_EMPTY[] COMPARING VBELN VLPLA.
  DELETE IT_BIN_EMPTY[] WHERE BIN_EMPTY_I EQ ''.
  IF  IT_BIN_EMPTY[] IS NOT INITIAL.                 " ++bharat on 28.06.2022
    MODIFY ZWM_PLESS_BIN FROM TABLE IT_BIN_EMPTY[].
  ENDIF.                                            " ++bharat on 28.06.2022
*End code

  SELECT SINGLE * FROM ZWM_HU_BIN  INTO LS_HU_BIN WHERE EXIDV = LV_EXIDV .
  IF SY-SUBRC IS INITIAL .
    EX_RETURN-TYPE = 'S'.
    EX_RETURN-MESSAGE_V1 = LS_HU_BIN-SAP_HU.
    CONCATENATE LS_HU_BIN-SAP_HU 'Created' INTO EX_RETURN-MESSAGE.
    RETURN.
  ENDIF.

  IF IT_DATA[] IS INITIAL .
    EX_RETURN-TYPE = C_ERROR.
    EX_RETURN-MESSAGE = 'No Data Scan'.
    RETURN.
  ENDIF.

** Comment start by bharat on 08.08.2022
*  IF line_exists( it_data[ rfbel = space ] ).
*    ex_return-type = c_error.
*    ex_return-message = |No bin assign for Article { it_data[ rfbel = space ]-matnr ALPHA = OUT }|.
*    RETURN.
*  ENDIF.
** comment end by bharat on 08.08.2022

*Checking Zero Qnty
  READ TABLE IT_DATA INTO LS_TMP WITH KEY TMENG = 0.
  IF SY-SUBRC = 0.
    EX_RETURN-TYPE = C_ERROR.
    EX_RETURN-MESSAGE = 'Zero Qnty found in Scan Data'.
    RETURN.
  ENDIF.

  LT_DATA[] = IT_DATA[].

*Delete duplicate material
*  LT_DATA = IT_DATA[].
  SORT LT_DATA BY P_MATERIAL.
  DELETE ADJACENT DUPLICATES FROM LT_DATA COMPARING P_MATERIAL.

*Generate Handling Unit
  IF LINES( LT_DATA ) GT 1.
    EX_RETURN-TYPE = C_ERROR.
    EX_RETURN-MESSAGE = 'Use only one packaging material at a time'.
    RETURN.
  ENDIF.

  PERFORM F_VALIDATE_FOR_QUANTITY USING L_VBELN L_EXIDV IT_DATA[] CHANGING EX_RETURN.
  IF EX_RETURN IS NOT INITIAL.
    RETURN.
  ENDIF.

* Create a HU without ITEMS
  CALL FUNCTION 'ZWM_UPDATE_PACKAGING'"""""""""""""""""" DESTINATION 'NONE' "commented 02-apr-26
    EXPORTING
      IM_LGNUM            = IM_LGNUM
      IM_VBELN            = L_VBELN
      IM_EXIDV            = '$1'
    IMPORTING
      EX_EXIDV            = L_EXIDV
    TABLES
      IT_DATA             = LT_DATA
    EXCEPTIONS
      UPDATE_NOT_POSSIBLE = 1
      OTHERS              = 2.

  IF SY-SUBRC <> 0.
    EX_RETURN-TYPE = C_ERROR.
    MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
            WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4 INTO EX_RETURN-MESSAGE.
    RETURN.
  ENDIF.

  LT_DATA[] = IT_DATA[].

  "commented 02-apr-26
  GET TIME STAMP FIELD L_TIME_STAMP.
  CALL FUNCTION 'ZWM_UPDATE_PACKAGING_BG'
    IN BACKGROUND TASK AS SEPARATE UNIT
    EXPORTING
      IM_LGNUM      = IM_LGNUM
      IM_VBELN      = L_VBELN
      IM_EXIDV      = L_EXIDV
      IM_USER       = L_USER
      IM_TIME_STAMP = L_TIME_STAMP
    TABLES
      IT_DATA       = LT_DATA.

*Could the background task initialized?
  CALL FUNCTION 'ID_OF_BACKGROUNDTASK'
    IMPORTING
      TID = LS_TID.

  IF LS_TID EQ SPACE.
    L_BKG_ERROR = 'X'.
    CALL FUNCTION 'BAPI_HU_DELETE'
      EXPORTING
        HUKEY  = L_EXIDV
      TABLES
        RETURN = LT_RETURN.

    EX_RETURN-TYPE = C_ERROR.
    EX_RETURN-MESSAGE = 'Error executing in background'(018).
    RETURN.
  ELSE.
    CALL FUNCTION 'STATUS_OF_BACKGROUNDTASK'
      EXPORTING
        TID           = LS_TID
      TABLES
        ERRORTAB      = LT_ERRORS
      EXCEPTIONS
        COMMUNICATION = 1
        RECORDED      = 2
        ROLLBACK      = 3
        OTHERS        = 4.

    IF  SY-SUBRC NE 0 AND SY-SUBRC NE 2.
      L_BKG_ERROR = 'X'.
      CALL FUNCTION 'BAPI_HU_DELETE'
        EXPORTING
          HUKEY  = L_EXIDV
        TABLES
          RETURN = LT_RETURN.
      EX_RETURN-TYPE = C_ERROR.
      EX_RETURN-MESSAGE = 'Error executing in background'(018).
      RETURN.
    ENDIF.
  ENDIF.
"commented 02-apr-26



  COMMIT WORK AND WAIT.
  SELECT SINGLE * FROM ZSTODO_DATA INTO @DATA(LS_STODO) WHERE VBELN = @IM_VBELN.

  IF LS_STODO-ZPLANT IS INITIAL .
    LS_STODO-ZPLANT = LS_STODO-WERKS.
  ENDIF.

  IF L_BKG_ERROR = ''.
    DATA: LS_TMP1 TYPE VEKP,
          LS_TMP2 TYPE VEPO.

*Check data updated against New HU - VKS-26.02.2021
    DO 10 TIMES.
      WAIT UP TO 1 SECONDS.
      SELECT SINGLE * FROM VEKP INTO LS_TMP1 WHERE EXIDV = L_EXIDV.
      IF SY-SUBRC = 0.
        SELECT SINGLE * FROM VEPO INTO LS_TMP2 WHERE VENUM = LS_TMP1-VENUM.
        IF SY-SUBRC = 0.
          EXIT.
        ENDIF.
      ENDIF.
      CLEAR: LS_TMP1, LS_TMP2.
    ENDDO.

*Check Blank,Zero Qty & Differ Qty Created HU - VKS-22.01.2021
    DATA: LS_VEKP TYPE VEKP,
          LS_VEPO TYPE VEPO,
          LT_VEPO TYPE TABLE OF VEPO,
          LV_FLAG TYPE C VALUE '',
          LS_TEMP TYPE VERPO,
          LV_QTY  TYPE TMENG.

"commented 02-apr-26
***************    SELECT SINGLE * FROM VEKP INTO LS_VEKP WHERE EXIDV = L_EXIDV.
***************    IF SY-SUBRC NE 0.
***************      SELECT SINGLE * FROM VEPO INTO LS_VEPO WHERE VENUM = LS_VEKP-VENUM.
***************      IF LS_VEPO IS INITIAL.
***************        DELETE FROM VEKP WHERE EXIDV = L_EXIDV.
***************        DELETE FROM VEPO WHERE VENUM = LS_VEKP-VENUM.
****************        DELETE FROM zwm_hudata WHERE sap_hu = l_exidv." --bharat on 28.06.2022
***************        DELETE FROM ZWM_HU_BIN WHERE SAP_HU = L_EXIDV.  " ++bharat on 28.06.2022
***************        EX_RETURN-TYPE = C_ERROR.
***************        EX_RETURN-MESSAGE = 'ERROR: HU is blank can not be saved, Try again'.
***************        LV_FLAG = 'X'.
***************        RETURN.
***************      ENDIF.
***************      CLEAR: LS_VEPO.
***************
***************      SELECT SINGLE * FROM VEPO INTO LS_VEPO WHERE VENUM = LS_VEKP-VENUM AND VEMNG = 0.
***************      IF SY-SUBRC = 0.
***************        DELETE FROM VEKP WHERE EXIDV = L_EXIDV.
***************        DELETE FROM VEPO WHERE VENUM = LS_VEKP-VENUM.
****************        DELETE FROM zwm_hudata WHERE sap_hu = l_exidv. "--bharat on 28.06.0222
***************        DELETE FROM ZWM_HU_BIN WHERE SAP_HU = L_EXIDV.  "  ++bharat on 28.06.2022
***************        EX_RETURN-TYPE = C_ERROR.
***************        EX_RETURN-MESSAGE = 'ERROR: Zero Qty HU can not be saved, Try again'.
***************        LV_FLAG = 'X'.
***************        RETURN.
***************      ENDIF.
***************      CLEAR: LS_VEPO.
***************
***************      SELECT venum vepos vbeln posnr matnr vemng meins
        FROM VEPO INTO TABLE LT_VEPO WHERE VENUM = LS_VEKP-VENUM.
***************      SORT LT_VEPO BY VBELN MATNR.  SORT LT_DATA BY VBELN MATNR.
***************      LOOP AT LT_VEPO INTO LS_VEPO WHERE VBELN = L_VBELN. "Parallel cussor
***************        READ TABLE IT_DATA INTO LS_TEMP WITH KEY VBELN = L_VBELN MATNR = LS_VEPO-MATNR BINARY SEARCH.
***************        IF SY-SUBRC = 0.
***************          LOOP AT IT_DATA INTO LS_TEMP FROM SY-TABIX.
***************            IF LS_TEMP-MATNR <> LS_VEPO-MATNR.
***************              EXIT.
***************            ENDIF.
***************            LV_QTY = LV_QTY + LS_TEMP-TMENG.
***************          ENDLOOP.
***************          IF LS_VEPO-VEMNG <> LV_QTY.
***************            DELETE FROM VEKP WHERE EXIDV = L_EXIDV.
***************            DELETE FROM VEPO WHERE VENUM = LS_VEKP-VENUM.
****************            DELETE FROM zwm_hudata WHERE sap_hu = l_exidv.  " --Bharat on 28.06.2022
***************            DELETE FROM ZWM_HU_BIN WHERE SAP_HU = L_EXIDV.  " ++Bharat on 28.06.2022
***************            EX_RETURN-TYPE = C_ERROR.
***************            EX_RETURN-MESSAGE = 'ERROR: Article Qty in Generated Int.HU & Scanning is not match, Try again'.
***************            LV_FLAG = 'X'.
***************            RETURN.
***************          ENDIF.
***************        ENDIF.
***************        CLEAR: LS_VEPO,LV_QTY.
***************      ENDLOOP.
***************      CLEAR: LS_VEKP.
***************    ENDIF.
"commented 02-apr-26


      IF L_BKG_ERROR = '' AND LV_FLAG = ''.
    WRITE L_EXIDV TO G_EXIDV.
    CONCATENATE 'Hu'(013) G_EXIDV 'Created Successfully'(011) '& submitted in background'(019) INTO EX_RETURN-MESSAGE SEPARATED BY SPACE.
    EX_RETURN-MESSAGE_V1 = G_EXIDV.
    LS_ZWM_HU_LOG-EXIDV = L_EXIDV.
    LS_ZWM_HU_LOG-ERNAM = L_USER.
    INSERT ZWM_HU_LOG FROM LS_ZWM_HU_LOG.

** Start bharat on 04.08.2022  Calculate Handling Unit weight
    IF GT_DATA IS NOT INITIAL.
      DATA(GT_DATA2) = GT_DATA .
      SORT GT_DATA2 ASCENDING BY MATNR.
      SELECT A~MATNR, SUM( A~BRGEW ) AS BRGEW FROM MARA AS A
          INNER JOIN @GT_DATA2 AS B
          ON  A~MATNR = B~MATNR
           GROUP BY A~MATNR
          INTO TABLE @DATA(GT_BRGEW).
    ENDIF.

    IF LT_DATA IS NOT INITIAL.
      DATA(LT_DATA2) = LT_DATA .
      SORT LT_DATA2 BY P_MATERIAL.
      DELETE ADJACENT DUPLICATES FROM LT_DATA2 COMPARING P_MATERIAL.
      SELECT A~MATNR, SUM( A~BRGEW ) AS BRGEW FROM MARA AS A
          INNER JOIN @LT_DATA2 AS B
          ON  A~MATNR = B~P_MATERIAL
           GROUP BY A~MATNR
          INTO TABLE @DATA(GT_BRGEW2).
    ENDIF.

    APPEND LINES OF GT_BRGEW TO GT_BRGEW2.
    LOOP AT GT_BRGEW2 INTO DATA(GW_BRGEW).
      GV_GRS_WT = GV_GRS_WT + GW_BRGEW-BRGEW.
    ENDLOOP.

    IF GV_GRS_WT IS NOT INITIAL.
      GV_GRS_WT = GV_GRS_WT / 1000.   " Convert GM to KG
    ENDIF.

    UPDATE VEKP SET: NTGEW = GV_GRS_WT GEWEI = 'KG' GEWEI_MAX = 'KG'
                  SEALN4 = LS_STODO-ZPLANT
           WHERE EXIDV = L_EXIDV.
    COMMIT WORK AND WAIT.
** end bharat on 04.08.2022

  ENDIF.
ELSE.
  ROLLBACK WORK.
  EX_RETURN-TYPE = C_ERROR.
  EX_RETURN-MESSAGE = ' Executing in background'(018).
  RETURN.
ENDIF.


SELECT SINGLE * FROM ZWM_EXREF INTO LS_EXREF WHERE EXIDV = LV_EXIDV.
*Update HU in table
  IF L_EXIDV IS NOT INITIAL AND LV_FLAG = ''.
    CLEAR: LV_EXIDV1.

    SELECT SINGLE SAP_HU FROM ZWM_EXREF INTO LV_EXIDV1  WHERE SAP_HU = L_EXIDV.
      IF SY-SUBRC = 0.
        EX_RETURN-TYPE = C_ERROR.
        EX_RETURN-MESSAGE = ' Internal Hu Already Exist'(023).
        RETURN.
      ELSE.
        UPDATE ZWM_EXREF SET: SAP_HU = L_EXIDV
                              ADATUM = SY-DATUM
                              HHT_ID = L_USER
                        WHERE EXIDV = LV_EXIDV.
        COMMIT WORK AND WAIT.
      ENDIF.
    ENDIF.
    IF IM_USER NE 'PTL'.
      CLEAR LV_POSNR .
      LOOP AT IT_DATA INTO LS_HHT_DATA .
        CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
          EXPORTING
            INPUT  = LS_HHT_DATA-MATNR
          IMPORTING
            OUTPUT = LS_HHT_DATA-MATNR.

        LV_POSNR = LV_POSNR + 1.
        LS_HU_BIN-SAP_HU = L_EXIDV .
        LS_HU_BIN-POSNR = LV_POSNR  .
        LS_HU_BIN-EXIDV = LV_EXIDV .
        LS_HU_BIN-VBELN = L_VBELN .
        LS_HU_BIN-HHT_USER = L_USER .
        LS_HU_BIN-WERKS = LS_EXREF-DWERKS.
        LS_HU_BIN-MATNR = LS_HHT_DATA-MATNR.
        LS_HU_BIN-MENGE = LS_HHT_DATA-TMENG.
        LS_HU_BIN-ABATCH = LS_STODO-ABATCH .
        LS_HU_BIN-LGPLA = LS_HHT_DATA-RFBEL.
        LS_HU_BIN-ERDAT = SY-DATUM.
        LS_HU_BIN-UZEIT = SY-UZEIT.

        LS_HU_BIN-STDATE = SY-DATUM ." LS_HHT_DATA-WDATU.
        LS_HU_BIN-STTIME = LS_HHT_DATA-RFPOS.


        APPEND LS_HU_BIN TO LT_HU_BIN .
        CLEAR LS_HU_BIN.

        LS_PICKLIST-SAP_HU = L_EXIDV .
        LS_PICKLIST-EXIDV = LV_EXIDV .
        LS_PICKLIST-ERNAM = L_USER .
        LS_PICKLIST-WERKS = LS_EXREF-DWERKS .
        LS_PICKLIST-CRATE =  '' .
        LS_PICKLIST-PROCESS =  'CLA' .
        LS_PICKLIST-DESTINATION =  'CLA' .
        LS_PICKLIST-MATNR =  LS_HHT_DATA-MATNR .
        LS_PICKLIST-PICKLISTNO =  L_VBELN .
        LS_PICKLIST-MENGE =  LS_HHT_DATA-TMENG.

        COLLECT LS_PICKLIST INTO IT_PICKLIST.
        CLEAR LS_PICKLIST.
        CLEAR LS_DATA .
      ENDLOOP.



      DATA : LS_HULOG TYPE ZWM_HU_STAT_LOG .
      LS_HULOG-EXIDV =  L_EXIDV.
      LS_HULOG-PROC_STAT = 'PICKING'.
      LS_HULOG-STATUS = 'CREATE'.
      LS_HULOG-STORE = LS_STODO-ZPLANT.
      LS_HULOG-SWERKS = LS_STODO-LIFNR.
      LS_HULOG-WERKS = LS_STODO-WERKS.

      CALL FUNCTION 'ZWM_RFC_STORE_HU_STATUS_SAVE'
        EXPORTING
          IM_USER = L_USER
          IM_DATA = LS_HULOG
* IMPORTING
*         EX_RETURN       =
        .


      DATA : EV_DOCNO TYPE ZZDOCNO.
      DATA : LS_RETURN TYPE BAPIRET2 .

      MODIFY ZWM_HU_BIN FROM TABLE  LT_HU_BIN  .
      CALL FUNCTION 'ZAPI_ADVERB_SAVE_CARTHU'
        IMPORTING
          EX_RETURN   = LS_RETURN
          EV_DOCNO    = EV_DOCNO
        TABLES
          IT_PICKLIST = IT_PICKLIST.
    ENDIF.

    SELECT SINGLE * FROM VEKP INTO LS_VEKP1 WHERE EXIDV = L_EXIDV.
      IF SY-SUBRC = 0 AND SY-TCODE = ''.
        LS_DATA-SWERKS = LS_VEKP1-WERKS.
        LS_DATA-VENUM  = LS_VEKP1-VENUM.
        LS_DATA-SAP_HU = LS_VEKP1-EXIDV.
        LS_DATA-VBELN  = L_VBELN.
        LS_DATA-DATUM  = SY-DATUM.
        LS_DATA-ERZET  = SY-UZEIT.
        LS_DATA-UNAME  = SY-UNAME.
        LS_DATA-TCODE  = 'HHT'.

*    IF SY-SUBRC = 0.
        LS_DATA-DWERKS = LS_EXREF-DWERKS.
        LS_DATA-EXIDV  = LS_EXREF-EXIDV.
*    ENDIF.

        SELECT venum vepos vbeln posnr matnr vemng meins
          FROM VEPO INTO TABLE LT_VEPO1 WHERE VENUM = LS_VEKP1-VENUM.
          LOOP AT LT_VEPO1 INTO LS_VEPO1.
            LV_HUQTY = LV_HUQTY + LS_VEPO1-VEMNG.
          ENDLOOP.
          LS_DATA-PQNTY  = LV_HUQTY.

          DELETE FROM ZBACKUPHU_DATA WHERE EXIDV = LS_DATA-EXIDV.
          MODIFY ZBACKUPHU_DATA FROM LS_DATA.
          CLEAR: LS_DATA,LS_VEPO1,LS_VEKP1,LV_HUQTY.
          REFRESH: LT_VEPO1.
        ENDIF.

*Paperless Picking Remove Del.from Drop Down after Complete Scan
        IF L_BKG_ERROR = '' AND LV_FLAG = ''.
          DATA: LT_LIPS      TYPE TABLE OF LIPS,
                LT_VEKP2     TYPE TABLE OF VEKP,
                LT_VEPO2     TYPE TABLE OF VEPO,
                LT_BIN_EMPTY TYPE TABLE OF ZWM_PLESS_BIN,
                LS_BIN_EMPTY TYPE ZWM_PLESS_BIN,
                LS_VEKP2     TYPE VEKP,
                LS_VEPO2     TYPE VEPO,
                LS_LIPS      TYPE LIPS,
                LS_ITDATA    TYPE VERPO.

          SELECT vbeln posnr matnr lfimg meins werks lgort
            FROM LIPS INTO TABLE LT_LIPS WHERE VBELN = IM_VBELN AND POSNR < 900000.
            IF SY-SUBRC = 0.
              SELECT vbeln matnr bin_empty_i
                FROM ZWM_PLESS_BIN INTO TABLE LT_BIN_EMPTY WHERE VBELN = IM_VBELN.
                IF SY-SUBRC = 0.
                  LOOP AT LT_BIN_EMPTY INTO LS_BIN_EMPTY WHERE BIN_EMPTY_I = 'X'. "Check Mark delete
                    DELETE LT_LIPS WHERE MATNR = LS_BIN_EMPTY-MATNR.
                  ENDLOOP.
                ENDIF.

                LOOP AT IT_DATA INTO LS_ITDATA. "Current Scan mins
                  CALL FUNCTION 'CONVERSION_EXIT_MATN1_INPUT'
                    EXPORTING
                      INPUT  = LS_ITDATA-MATNR
                    IMPORTING
                      OUTPUT = LS_ITDATA-MATNR.

                  READ TABLE LT_LIPS INTO LS_LIPS WITH KEY MATNR = LS_ITDATA-MATNR.
                  IF SY-SUBRC = 0.
                    LS_LIPS-LFIMG = LS_LIPS-LFIMG - LS_ITDATA-TMENG.
                    MODIFY LT_LIPS FROM LS_LIPS INDEX SY-TABIX.
                  ENDIF.
                ENDLOOP.

                SELECT exidv venum status vpobj vpobjkey brgew
                  FROM VEKP INTO TABLE LT_VEKP2 WHERE VBELN_GEN = IM_VBELN "Old Partial Del.mins
                  AND EXIDV <> L_EXIDV %_HINTS ORACLE 'INDEX("VEKP" "VEPO~ZEK")'.
                  IF SY-SUBRC = 0.
                    SELECT venum vepos vbeln posnr matnr vemng meins
                      FROM VEPO INTO TABLE LT_VEPO2 FOR ALL ENTRIES IN LT_VEKP2
                      WHERE VENUM = LT_VEKP2-VENUM.
                      IF SY-SUBRC = 0.
                        LOOP AT LT_VEPO2 INTO LS_VEPO2.
                          CALL FUNCTION 'CONVERSION_EXIT_MATN1_INPUT'
                            EXPORTING
                              INPUT  = LS_VEPO2-MATNR
                            IMPORTING
                              OUTPUT = LS_VEPO2-MATNR.

                          READ TABLE LT_LIPS INTO LS_LIPS WITH KEY MATNR = LS_VEPO2-MATNR.
                          IF SY-SUBRC = 0.
                            LS_LIPS-LFIMG = LS_LIPS-LFIMG - LS_VEPO2-VEMNG.
                            MODIFY LT_LIPS FROM LS_LIPS INDEX SY-TABIX.
                          ENDIF.
                        ENDLOOP.
                      ENDIF.
                    ENDIF.

                    DELETE LT_LIPS WHERE LFIMG = 0.
                    IF LT_LIPS IS INITIAL.
                      UPDATE ZWM_DEL_HHT_MAP SET DLVFLAG = 'X' WHERE VBELN = IM_VBELN.
                    ENDIF.

                  ENDIF.
                ENDIF.
                REFRESH IT_DATA .
              ENDFUNCTION.