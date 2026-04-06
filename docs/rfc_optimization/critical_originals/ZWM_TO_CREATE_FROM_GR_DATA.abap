FUNCTION ZWM_TO_CREATE_FROM_GR_DATA.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_MBLNR) TYPE  EBELN
*"     VALUE(IM_MJAHR) TYPE  MJAHR
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"  TABLES
*"      IT_DATA STRUCTURE  MSEG OPTIONAL
*"----------------------------------------------------------------------
*
  DATA:
    LT_MSEG  TYPE STANDARD TABLE OF MSEG INITIAL SIZE 0,
    LT_DATA  TYPE STANDARD TABLE OF MSEG INITIAL SIZE 0,
    LT_LTBP  TYPE STANDARD TABLE OF LTBP INITIAL SIZE 0,
    LS_MKPF  TYPE MKPF,
    LR_MSEG  TYPE REF TO MSEG,
    LR_DATA  TYPE REF TO MSEG,
    LR_DATA2 TYPE REF TO MSEG,
    L_INDX   TYPE SYTABIX,
    LS_TRITE TYPE L03B_TRITE,
    LT_TRITE TYPE L03B_TRITE_T,
    L_TANUM  TYPE TANUM,
    L_TBNUM  TYPE TBNUM,
    L_LGNUM  TYPE LGNUM.

  DATA: LV_LGTYP TYPE LGTYP,              ""02.02.2026 15:45:16""Added by Priya Skyper""
        LV_NLTYP TYPE LTAP_NLTYP.          ""02.02.2026 15:45:21""Added by Priya Skyper""

  BREAK-POINT ID Z_V2CHECK.

  SELECT SINGLE MJAHR FROM  MKPF INTO @DATA(LV_MJAHR) WHERE MBLNR = @IM_MBLNR.


  CALL FUNCTION 'ZWM_GR_GET_HISTORY'
    EXPORTING
      IM_MBLNR      = IM_MBLNR
      IM_MJAHR      = LV_MJAHR
    IMPORTING
      EX_MKPF       = LS_MKPF
    TABLES
      ET_MSEG       = LT_MSEG
      ET_LTBP       = LT_LTBP
    EXCEPTIONS
      ERROR_MESSAGE = 1
      OTHERS        = 2.

  IF SY-SUBRC <> 0.
    EX_RETURN-TYPE = C_ERROR.
    MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
          WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4
          INTO EX_RETURN-MESSAGE.
    RETURN.
  ENDIF.

  DELETE LT_MSEG WHERE TBNUM IS INITIAL OR ERFMG IS INITIAL.

  SORT LT_MSEG BY MATNR ZEILE.

  LOOP AT IT_DATA REFERENCE INTO LR_DATA.

    TRANSLATE LR_DATA->LGPLA TO UPPER CASE .
    READ TABLE LT_DATA REFERENCE INTO LR_DATA2
    WITH KEY MATNR = LR_DATA->MATNR
             LGPLA = LR_DATA->LGPLA.

    IF SY-SUBRC IS NOT INITIAL.
      APPEND INITIAL LINE TO LT_DATA REFERENCE INTO LR_DATA2.
      MOVE LR_DATA->* TO LR_DATA2->*.
      CLEAR LR_DATA2->MENGE.
    ENDIF.

    LR_DATA2->MENGE = LR_DATA2->MENGE + LR_DATA->MENGE.
  ENDLOOP.


  LOOP AT LT_DATA REFERENCE INTO LR_DATA.

    CLEAR:
       L_INDX.

*    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
*      EXPORTING
*        INPUT  = LR_DATA->MATNR
*      IMPORTING
*        OUTPUT = LR_DATA->MATNR.

    READ TABLE LT_MSEG TRANSPORTING NO FIELDS
    WITH KEY MATNR = LR_DATA->MATNR
    BINARY SEARCH.

    IF SY-SUBRC IS INITIAL.
      L_INDX = SY-TABIX.

      LOOP AT LT_MSEG REFERENCE INTO LR_MSEG FROM L_INDX.

        IF LR_MSEG->MATNR NE LR_DATA->MATNR OR
           LR_DATA->MENGE IS INITIAL.
          EXIT.
        ENDIF.

**        SELECT SINGLE A~LGTYP FROM ZWM_DC_MASTER AS A INTO @LV_LGTYP WHERE A~WERKS = @LR_MSEG->WERKS.   ""Commented by Priya Skyper02.02.2026 16:30:03""

        ""02.02.2026 15:43:31 Added By Priya .

        SELECT SINGLE A~LGTYP,
                      A~NLTYP
          FROM ZWM_DC_MASTER AS A
          INTO (@LV_LGTYP , @LV_NLTYP) WHERE A~WERKS = @LR_MSEG->WERKS.


        ""% End Added By Priya.""



        L_TBNUM        = LR_MSEG->TBNUM.
        L_LGNUM        = LR_MSEG->LGNUM.
        LS_TRITE-ALTME = LR_MSEG->MEINS.
        LS_TRITE-VLBER = '001'.
        LS_TRITE-NLPLA =  LR_DATA->LGPLA. "lr_mseg->lgpla.
        LS_TRITE-NLTYP =  LV_LGTYP.

**** destination bin
        LS_TRITE-NLBER = '001'.
        LS_TRITE-VLTYP = LR_MSEG->LGTYP.
        LS_TRITE-VLPLA = LR_MSEG->LGPLA .

        IF LR_DATA->MENGE LE LR_MSEG->ERFMG.
          LS_TRITE-ANFME  =  LR_DATA->MENGE.
          CLEAR LR_DATA->MENGE.
        ELSE.
          LS_TRITE-ANFME =  LR_MSEG->ERFMG.
          LR_DATA->MENGE = LR_DATA->MENGE - LR_MSEG->ERFMG.
        ENDIF.

        LS_TRITE-TBPOS = LR_MSEG->TBPOS.
*        ls_trite-anfme = lr_mseg->menge .
        APPEND LS_TRITE TO LT_TRITE.

        CLEAR:
           LS_TRITE.

      ENDLOOP.
    ENDIF.

  ENDLOOP.

  ""02.02.2026 15:29:52 Added By Priya .

  DATA: LS_LAGP_CHECK TYPE LAGP.

  LOOP AT LT_DATA REFERENCE INTO LR_DATA.

    SELECT SINGLE
           _L1~LGNUM,
           _L1~LGTYP AS E_TYPE,
           _L1~LGPLA AS E_BIN,
           _L1~KZLER AS E_EMPTY,
           _L2~LGTYP AS G_TYPE,
           _L2~KZLER AS G_EMPTY
      FROM LAGP AS _L1
      LEFT JOIN LAGP AS _L2
        ON _L1~LGPLA = _L2~LGPLA
       AND _L1~LGNUM = _L2~LGNUM
       AND _L2~LGTYP = @LV_NLTYP
      WHERE _L1~LGNUM = 'V2R'
        AND _L1~LGPLA = @LR_DATA->LGPLA
        AND _L1~LGTYP = @LV_LGTYP
      INTO @DATA(T_LAGP).

    IF SY-SUBRC <> 0.
      EX_RETURN-TYPE = 'E'.
      EX_RETURN-MESSAGE = |Bin { LR_DATA->LGPLA } does not exist|.
      RETURN.
    ENDIF.

    "--- Empty bin validation
"---------------------------------------------------------------------------------------------------------------
*"-----Below code commented on 10.03.2026 by anuj as suggested by Bhavesh Sir and Anshul WM consultant---------"
"---------------------------------------------------------------------------------------------------------------
***    IF T_LAGP-G_EMPTY = '' OR T_LAGP-E_EMPTY = ''.
***
***      IF T_LAGP-G_EMPTY = '' AND T_LAGP-G_TYPE IS NOT INITIAL.
***        EX_RETURN-TYPE = 'E'.
***        EX_RETURN-MESSAGE =
***          |Bin { T_LAGP-E_BIN } is not empty in storage type { LV_NLTYP }|.
***        RETURN.
***
***      ELSEIF T_LAGP-E_EMPTY = '' AND  T_LAGP-E_TYPE IS NOT INITIAL.
***        EX_RETURN-TYPE = 'E'.
***        EX_RETURN-MESSAGE =
***          |Bin { T_LAGP-E_BIN } is not empty in storage type { LV_LGTYP }|.
***        RETURN.
***      ENDIF.
***
***    ENDIF.
*"-----code commented end on 10.03.2026 by anuj as suggested by Bhavesh Sir and Anshul WM consultant---------"


********  --- Check destination storage type exists
***********    IF T_LAGP-G_TYPE IS INITIAL AND T_LAGP- IS NOT INITIAL.
***********      EX_RETURN-TYPE = 'E'.
***********      EX_RETURN-MESSAGE =
***********        |Bin { T_LAGP-E_BIN } not maintained in storage type { LV_NLTYP }|.
***********      RETURN.
***********    ENDIF.

  ENDLOOP.


  ""% End Added By Priya.""

  PERFORM GET_ARFC_RESSOURCES.

  G_JOBS = G_JOBS - 1 .

  CALL FUNCTION 'L_TO_CREATE_TR'
    STARTING NEW TASK 'NEW_TASK'
    PERFORMING CHECK_RESULT_TR_TO ON END OF TASK
    EXPORTING
      I_LGNUM       = L_LGNUM
      I_TBNUM       = L_TBNUM
      I_COMMIT_WORK = 'X'
      I_SQUIT       = 'X'
      I_BNAME       = SY-UNAME
      IT_TRITE      = LT_TRITE.


  WAIT UNTIL G_COMP >= 1.

  L_TANUM = G_TANUM.

  IF G_SYS-SUBRC <> 0.
    EX_RETURN-TYPE = C_ERROR.
    MESSAGE ID G_SYS-MSGID TYPE G_SYS-MSGTY NUMBER G_SYS-MSGNO
    WITH G_SYS-MSGV1 G_SYS-MSGV2 G_SYS-MSGV3 G_SYS-MSGV4
    INTO EX_RETURN-MESSAGE.
    RETURN.
  ENDIF.


  IF L_TANUM IS NOT INITIAL.
    SORT LT_DATA BY LGPLA .
    DELETE ADJACENT DUPLICATES FROM LT_DATA COMPARING LGPLA .
    LOOP AT LT_DATA REFERENCE INTO LR_DATA.

      UPDATE LAGP SET LZONE = '' LPTYP = 'F1'
                           WHERE LGNUM = 'V2R'
                           AND LGTYP = LV_LGTYP
                           AND LGPLA = LR_DATA->LGPLA .
    ENDLOOP.

    EX_RETURN-TYPE = C_SUCCESS.
    MESSAGE S016(L3) WITH L_TANUM INTO EX_RETURN-MESSAGE.
  ELSE.
    EX_RETURN-TYPE = C_ERROR.
    EX_RETURN-MESSAGE = 'No to Created'(010)..
  ENDIF.


ENDFUNCTION.