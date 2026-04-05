FUNCTION ZWM_APP_ARTICLE_DETAILS.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_EAN) TYPE  EAN11 OPTIONAL
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_LGNUM) TYPE  LGNUM DEFAULT 'SDC'
*"     VALUE(IM_GEN) TYPE  CHAR1 DEFAULT 'X'
*"  EXPORTING
*"     VALUE(ES_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_DATA TYPE  ZWM_ST_ARTICLE_DETAILS_T OPTIONAL
*"----------------------------------------------------------------------
BREAK-POINT ID Z_V2CHECK.
  DATA :  LS_MEAN TYPE MEAN ,
          LS_MARD TYPE MARD,
          LS_LQUA TYPE LQUA,
          LS_MARA TYPE MARA,
          LS_MAKT TYPE MAKT,
          LV_LGTYP TYPE LGTYP,
          LV_GEN  TYPE CHAR1,
          LS_DATA TYPE ZWM_ST_ARTICLE_DETAILS,
          LV_EAN TYPE EAN11.


  DATA : LT_MARD TYPE STANDARD TABLE OF MARD,
         LT_LQUA TYPE STANDARD TABLE OF LQUA,
         LT_DATA  TYPE ZWM_ST_ARTICLE_DETAILS_T,
         LT_DATA2  TYPE ZWM_ST_ARTICLE_DETAILS_T.


  DATA : LV_MATNR TYPE MATNR .
  DATA LS_MSEG4 TYPE TY_MSEG1.

  DATA : REF_DATA TYPE REF TO ZWM_ST_ARTICLE_DETAILS.


********************************** Sale Order *********************************

  DATA : LT_MAKT TYPE STANDARD TABLE OF MAKT .

  DATA :
         LS_DATA_TMP TYPE ZWM_APP_SALES_DATA,
         LT_DATA_TMP TYPE STANDARD TABLE OF ZWM_APP_SALES_DATA,
         LT_VBRP TYPE STANDARD TABLE OF ZVBRP_VRPMA,
         LS_VBRP TYPE ZVBRP_VRPMA,
         E_FY TYPE CHAR4,
         LV_PRE_M_DT TYPE DATUM,
         LV_PRE_M_LT TYPE DATUM,
         LV_PREV_DATE TYPE DATUM,
         LV_KUNNR TYPE KUNNR.


*PERFORM f_fetch_data4
*          IN PROGRAM ('SAPLZWM_APP')
*          USING '0008' '0001' im_werks.

  IF IM_EAN IS INITIAL .
    ES_RETURN-MESSAGE  = 'Barcode empty'.
    ES_RETURN-TYPE = 'E'.
    EXIT.
  ENDIF.

  IF IM_WERKS IS INITIAL .
    ES_RETURN-MESSAGE  = 'Site empty'.
    ES_RETURN-TYPE = 'E'.
    EXIT.
  ENDIF.

  IF IM_LGNUM IS INITIAL .
    ES_RETURN-MESSAGE  = 'ware house empty'.
    ES_RETURN-TYPE = 'E'.
    EXIT.
  ENDIF.



  LV_GEN = IM_GEN.
  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_EAN
    IMPORTING
      OUTPUT = LV_EAN.


  SELECT SINGLE * FROM MEAN INTO LS_MEAN
              WHERE EAN11 = IM_EAN.
  IF SY-SUBRC IS NOT INITIAL .
    ES_RETURN-MESSAGE  = 'Invalid Barcode'.
    ES_RETURN-TYPE = 'E'.
    EXIT.
  ELSE.

    SELECT SINGLE matnr matkl mtart satnr size1 color attyp meins
      FROM MARA INTO LS_MARA
                WHERE MATNR = LS_MEAN-MATNR
                   AND ATTYP = '02'.
    IF SY-SUBRC IS NOT INITIAL.
      LV_GEN = ''.
    ENDIF.
  ENDIF.

  IF LV_GEN IS INITIAL.
    LS_DATA-MATNR = LS_MEAN-MATNR.
    SELECT lgnum matnr werks lgtyp lgpla lgort verme meins
      FROM LQUA INTO TABLE LT_LQUA
                  WHERE LGNUM = IM_LGNUM
                    AND WERKS = IM_WERKS
                    AND MATNR = LS_MEAN-MATNR.

    SELECT matnr werks lgort labst insme speme
      FROM MARD INTO TABLE LT_MARD WHERE MATNR = LS_MEAN-MATNR
                                            AND WERKS = IM_WERKS .
  ELSE.

    CONCATENATE LS_MEAN-MATNR+0(15) '%' INTO LV_MATNR.
    LS_DATA-MATNR = LS_MEAN-MATNR+0(15).
    SELECT lgnum matnr werks lgtyp lgpla lgort verme meins
      FROM LQUA INTO TABLE LT_LQUA
                    WHERE LGNUM = IM_LGNUM
                      AND WERKS = IM_WERKS
                      AND MATNR LIKE LV_MATNR.

    SELECT matnr werks lgort labst insme speme
      FROM MARD INTO TABLE LT_MARD WHERE MATNR LIKE  LV_MATNR
                                            AND WERKS = IM_WERKS .

  ENDIF.


  IF LT_LQUA[] IS INITIAL AND LT_MARD[] IS  INITIAL.
    ES_RETURN-MESSAGE  = 'No data Found'.
    ES_RETURN-TYPE = 'E'.
    EXIT.
  ENDIF.

*  ls_data-ean11 = ls_mean-ean11.
  LS_DATA-WERKS = IM_WERKS.
  LS_DATA-LGNUM = IM_LGNUM.

  LV_LGTYP = IM_WERKS+1(3).





  LOOP AT LT_LQUA INTO LS_LQUA .

    LS_DATA-MATNR = LS_LQUA-MATNR .
    LS_DATA-WERKS = IM_WERKS.
    LS_DATA-LGNUM = IM_LGNUM.

    SELECT SINGLE matnr matkl mtart satnr size1 color attyp meins
      FROM MARA INTO LS_MARA WHERE MATNR = LS_LQUA-MATNR .
    IF SY-SUBRC IS INITIAL.
      LS_DATA-MATKL = LS_MARA-MATKL.
      LS_DATA-SIZE1 = LS_MARA-SIZE1.
      LS_DATA-COLOR = LS_MARA-COLOR.
    ENDIF.

    SELECT SINGLE matnr spras maktx
      FROM MAKT INTO LS_MAKT WHERE MATNR = LS_LQUA-MATNR
                                            AND SPRAS = SY-LANGU .
    IF SY-SUBRC IS INITIAL .
      LS_DATA-MAKTX = LS_MAKT-MAKTX.
    ENDIF .
    CASE LS_LQUA-LGTYP.
      WHEN 'V01'.
*        ls_data-v01 =  ls_data-v01 + ls_lqua-verme.
        LS_DATA-V01 =  LS_LQUA-VERME.
      WHEN 'V04'.
*        ls_data-v04 =  ls_data-v04 + ls_lqua-verme.
        LS_DATA-V04 =   LS_LQUA-VERME.
      WHEN 'V11'.
      WHEN LV_LGTYP .
*        ls_data-msa =  ls_data-msa + ls_lqua-verme.
        LS_DATA-MSA =   LS_LQUA-VERME.
    ENDCASE.

    COLLECT LS_DATA INTO LT_DATA.
    CLEAR LS_DATA .
  ENDLOOP.

  SORT LT_DATA BY MATNR .
  LOOP AT LT_MARD INTO LS_MARD.

    READ TABLE LT_DATA INTO LS_DATA WITH KEY MATNR = LS_MARD-MATNR
                                              BINARY SEARCH .
    IF SY-SUBRC IS INITIAL .

    ELSE.
      LS_DATA-WERKS = IM_WERKS.
      LS_DATA-LGNUM = IM_LGNUM.
      LS_DATA-MATNR = LS_MARD-MATNR .
      SELECT SINGLE matnr matkl mtart satnr size1 color attyp meins
        FROM MARA INTO LS_MARA WHERE MATNR = LS_MARD-MATNR .
      IF SY-SUBRC IS INITIAL .
        LS_DATA-MATKL = LS_MARA-MATKL .
        LS_DATA-SIZE1 = LS_MARA-SIZE1.
        LS_DATA-COLOR = LS_MARA-COLOR.
      ENDIF.

      SELECT SINGLE matnr spras maktx
        FROM MAKT INTO LS_MAKT WHERE MATNR = LS_MARD-MATNR
                                              AND SPRAS = SY-LANGU .
      IF SY-SUBRC IS INITIAL .
        LS_DATA-MAKTX = LS_MAKT-MAKTX.
      ENDIF .
    ENDIF.

    CASE LS_MARD-LGORT .
      WHEN '0001'.
*        ls_data-0001 = ls_data-0001 + ls_mard-labst.
        LS_DATA-0001 =  LS_MARD-LABST.

      WHEN '0003'.
*        ls_data-0003 = ls_data-0003 + ls_mard-labst.
        LS_DATA-0003 = LS_MARD-LABST.
      WHEN '0004'.
*        ls_data-0004 = ls_data-0004 + ls_mard-labst.
        LS_DATA-0004 = LS_MARD-LABST.
      WHEN '0005'.
*        ls_data-0005 = ls_data-0005 + ls_mard-labst.
        LS_DATA-0005 =  LS_MARD-LABST.
      WHEN '0006'.
*        ls_data-0006 = ls_data-0006 + ls_mard-labst.
        LS_DATA-0006 = LS_MARD-LABST.
      WHEN '0007'.
*        ls_data-0007 = ls_data-0007 + ls_mard-labst.
        LS_DATA-0007 =  LS_MARD-LABST.
      WHEN '0008'.
*        ls_data-0008 = ls_data-0008 + ls_mard-labst.
        LS_DATA-0008 =  LS_MARD-LABST.
      WHEN '0009'.
*        ls_data-0009 = ls_data-0008 + ls_mard-labst.
        LS_DATA-0009 =  LS_MARD-LABST.
      WHEN '0010'.
*        ls_data-0010 = ls_data-0008 + ls_mard-labst.
        LS_DATA-0010 = LS_MARD-LABST.
    ENDCASE.

    COLLECT LS_DATA INTO LT_DATA2.
    CLEAR LS_DATA.
  ENDLOOP.


  LOOP AT LT_DATA2 INTO LS_DATA.

    READ TABLE LT_DATA REFERENCE INTO REF_DATA WITH KEY MATNR = LS_DATA-MATNR
                                                        COMP = ''.
*                                                    BINARY SEARCH .
    IF SY-SUBRC IS INITIAL .
      LS_DATA-V01 =  REF_DATA->V01.
      LS_DATA-V04 =  REF_DATA->V04.
      LS_DATA-MSA =  REF_DATA->MSA.
      REF_DATA->COMP = 'X'.
    ENDIF.

    COLLECT LS_DATA INTO ET_DATA .
    CLEAR LS_DATA .
  ENDLOOP.


  LOOP AT LT_DATA INTO LS_DATA WHERE COMP IS INITIAL .
    LS_DATA-WERKS = IM_WERKS.
    LS_DATA-LGNUM = IM_LGNUM.
    LS_DATA-MATNR = LS_DATA-MATNR .
    LS_DATA-MAKTX = LS_DATA-MAKTX .
    LS_DATA-MATKL = LS_DATA-MATKL .
    LS_DATA-V01 =  REF_DATA->V01.
    LS_DATA-V04 =  REF_DATA->V04.
    LS_DATA-MSA =  REF_DATA->MSA.
    REF_DATA->COMP = 'X'.

    COLLECT LS_DATA INTO ET_DATA .
    CLEAR LS_DATA .
  ENDLOOP.


ENDFUNCTION.