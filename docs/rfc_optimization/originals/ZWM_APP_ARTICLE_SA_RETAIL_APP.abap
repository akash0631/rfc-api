FUNCTION ZWM_APP_ARTICLE_SA_RETAIL_APP.
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
*"      ET_DATA TYPE  ZWM_ST_SA_DATA_RETAIL_APP_TT OPTIONAL
*"      ET_DATA_SUM TYPE  ZWM_ST_SA_DATA_RETAIL_APP_TT OPTIONAL
*"----------------------------------------------------------------------
  BREAK-POINT ID Z_V2CHECK.
  DATA : LS_MEAN      TYPE MEAN,
         LS_MARD      TYPE MARD,
         LS_MARC      TYPE MARC,
         LS_LQUA      TYPE LQUA,
         LS_MARA      TYPE MARA,
         LS_MAKT      TYPE MAKT,
         LV_LGTYP     TYPE LGTYP,
         LV_GEN       TYPE CHAR1,
         LS_DATA      TYPE ZWM_ST_SA_DATA_RETAIL_APP,
         LV_EAN       TYPE EAN11,
         LV_INTRANSIT TYPE CHAR20.


  DATA : LT_MARD  TYPE STANDARD TABLE OF MARD,
         LT_MARC  TYPE STANDARD TABLE OF MARC,
         LT_LQUA  TYPE STANDARD TABLE OF LQUA,
         LT_DATA  TYPE  ZWM_ST_SA_DATA_RETAIL_APP_TT,
         LT_DATA2 TYPE  ZWM_ST_SA_DATA_RETAIL_APP_TT.


  DATA : LV_MATNR TYPE MATNR .
  DATA : LV_TOT TYPE CHAR4.
  DATA LS_MSEG4 TYPE TY_MSEG1.

  DATA : REF_DATA TYPE REF TO ZWM_ST_SA_DATA_RETAIL_APP.


********************************** Sale Order *********************************

  DATA : LT_MAKT TYPE STANDARD TABLE OF MAKT .

  DATA :
    LS_DATA_TMP     TYPE ZWM_APP_SALES_DATA,
    LT_DATA_TMP     TYPE STANDARD TABLE OF ZWM_APP_SALES_DATA,
    LT_VBRP         TYPE STANDARD TABLE OF ZVBRP_VRPMA,
    IT_VBRP         TYPE STANDARD TABLE OF VRPMA,
    LS_VBRP         TYPE ZVBRP_VRPMA,
    WA_VBRP         TYPE VRPMA,
    E_FY            TYPE CHAR4,
    LV_MONTH        TYPE NUMC2,
    LV_PRE_M_DT     TYPE DATUM,
    LV_PRE_M_FIR_DT TYPE DATUM,
    LV_PRE_M_LT     TYPE DATUM,
    LV_PREV_DATE    TYPE DATUM,
    LV_KUNNR        TYPE KUNNR.


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

    SELECT SINGLE * FROM MARA INTO LS_MARA
                WHERE MATNR = LS_MEAN-MATNR
                   AND ATTYP = '02'.
    IF SY-SUBRC IS NOT INITIAL.
      LV_GEN = ''.
    ENDIF.
  ENDIF.
  SELECT * FROM MARC INTO TABLE LT_MARC
             WHERE MATNR = LS_MEAN-MATNR
             AND   WERKS = IM_WERKS.

  IF LV_GEN IS INITIAL.
    LS_DATA-MATNR = LS_MEAN-MATNR.
    SELECT * FROM LQUA INTO TABLE LT_LQUA
                  WHERE LGNUM = IM_LGNUM
                    AND WERKS = IM_WERKS
                    AND MATNR = LS_MEAN-MATNR.

    SELECT * FROM MARD INTO TABLE LT_MARD WHERE MATNR = LS_MEAN-MATNR
                                            AND WERKS = IM_WERKS .
  ELSE.

    CONCATENATE LS_MEAN-MATNR+0(15) '%' INTO LV_MATNR.
    LS_DATA-MATNR = LS_MEAN-MATNR+0(15).
    SELECT * FROM LQUA INTO TABLE LT_LQUA
                    WHERE LGNUM = IM_LGNUM
                      AND WERKS = IM_WERKS
                      AND MATNR LIKE LV_MATNR
                      AND LGORT = '0002'.


    SELECT * FROM MARD INTO TABLE LT_MARD WHERE MATNR LIKE  LV_MATNR
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

  DATA(LT_LQUA_T) = LT_LQUA.
  DATA(LT_MARD_T) = LT_MARD.
  SORT LT_LQUA_T BY MATNR WERKS.
  SORT LT_MARD_T BY MATNR WERKS.
  DELETE ADJACENT DUPLICATES FROM LT_LQUA_T COMPARING MATNR WERKS.
  DELETE ADJACENT DUPLICATES FROM LT_MARD_T COMPARING MATNR WERKS.

  SELECT A~MATNR,
         A~WERKS,
         A~IROD,
         A~MTVER
   FROM ZZMARC AS A
  INNER JOIN @LT_LQUA_T AS B
     ON A~MATNR EQ B~MATNR
    AND A~WERKS EQ B~WERKS
INTO TABLE @DATA(LT_ZMARC).

  SELECT A~MATNR,
         A~WERKS,
         A~IROD
   FROM ZZMARC AS A
  INNER JOIN @LT_MARD_T AS B
     ON A~MATNR EQ B~MATNR
    AND A~WERKS EQ B~WERKS
 APPENDING TABLE @LT_ZMARC.

  SORT LT_ZMARC BY MATNR WERKS.
  DELETE ADJACENT DUPLICATES FROM LT_ZMARC.

  LOOP AT LT_LQUA INTO LS_LQUA .

    LS_DATA-MATNR = LS_LQUA-MATNR .
    LS_DATA-WERKS = IM_WERKS.
    LS_DATA-LGNUM = IM_LGNUM.

    READ TABLE LT_ZMARC ASSIGNING FIELD-SYMBOL(<LFS_ZMARC>)
     WITH KEY MATNR = LS_LQUA-MATNR
              WERKS = LS_LQUA-WERKS
          BINARY SEARCH.

    IF <LFS_ZMARC> IS ASSIGNED.
      LS_DATA-IRODE = <LFS_ZMARC>-IROD.
      LS_DATA-MATKL = <LFS_ZMARC>-MTVER.
    ENDIF.
    UNASSIGN <LFS_ZMARC>.

    SELECT SINGLE * FROM MARA INTO LS_MARA WHERE MATNR = LS_LQUA-MATNR .
    IF SY-SUBRC IS INITIAL.
      SHIFT LS_MARA-SATNR LEFT DELETING LEADING '0'.
      LS_DATA-SATNR = LS_MARA-SATNR.
      LS_DATA-SIZE1 = LS_MARA-SIZE1.
      LS_DATA-COLOR = LS_MARA-COLOR.
    ENDIF.

    SELECT SINGLE * FROM MAKT INTO LS_MAKT WHERE MATNR = LS_LQUA-MATNR
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
*        ls_data-v11 =   ls_lqua-verme.
      WHEN LV_LGTYP.
        LS_DATA-V02 =   LS_LQUA-VERME.
**        LS_LQUA-LGORT = '0002'.
*
*   lv_tot = ls_lqua-verme + ls_data-v04.
*       ls_data-msa = lv_tot.
    ENDCASE.

    SELECT SINGLE * FROM MARC INTO LS_MARC WHERE MATNR = LS_LQUA-MATNR
                                                AND WERKS = IM_WERKS.

    IF SY-SUBRC = 0.
      LV_INTRANSIT = LS_MARC-UMLMC + LS_MARC-TRAME.
      LS_DATA-INTRANSIT = LV_INTRANSIT.

    ENDIF.


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
      SELECT SINGLE * FROM MARA INTO LS_MARA WHERE MATNR = LS_MARD-MATNR .
      IF SY-SUBRC IS INITIAL .
*        ls_data-matkl = ls_mara-matkl .
        LS_DATA-SIZE1 = LS_MARA-SIZE1.
        LS_DATA-COLOR = LS_MARA-COLOR.
      ENDIF.

      SELECT SINGLE * FROM MAKT INTO LS_MAKT WHERE MATNR = LS_MARD-MATNR
                                              AND SPRAS = SY-LANGU .
      IF SY-SUBRC IS INITIAL .
        LS_DATA-MAKTX = LS_MAKT-MAKTX.
      ENDIF .

      READ TABLE LT_ZMARC ASSIGNING <LFS_ZMARC>
       WITH KEY MATNR = LS_MARD-MATNR
                WERKS = LS_MARD-WERKS
            BINARY SEARCH.
      IF <LFS_ZMARC> IS ASSIGNED.
        LS_DATA-IRODE = <LFS_ZMARC>-IROD.
        LS_DATA-MATKL = <LFS_ZMARC>-MTVER.
      ENDIF.
      UNASSIGN <LFS_ZMARC>.

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
      LS_DATA-V02 =  REF_DATA->V02.
      LS_DATA-INTRANSIT = REF_DATA->INTRANSIT.
      REF_DATA->COMP = 'X'.
    ENDIF.

    COLLECT LS_DATA INTO ET_DATA .
    CLEAR LS_DATA .
  ENDLOOP.


  LOOP AT LT_DATA INTO LS_DATA WHERE COMP IS INITIAL .
    LS_DATA-WERKS = IM_WERKS.
    LS_DATA-LGNUM = IM_LGNUM.
    LS_DATA-MATNR = LS_DATA-MATNR .
    LS_DATA-SIZE1 = LS_DATA-SIZE1 .
    LS_DATA-COLOR = LS_DATA-COLOR .
    LS_DATA-MAKTX = LS_DATA-MAKTX .
    LS_DATA-MATKL = LS_DATA-MATKL .

    LS_DATA-V01 =  REF_DATA->V01.
    LS_DATA-V04 =  REF_DATA->V04.
    LS_DATA-V02 =  REF_DATA->V02.
    LS_DATA-INTRANSIT = REF_DATA->INTRANSIT.
    REF_DATA->COMP = 'X'.

    COLLECT LS_DATA INTO ET_DATA .
    CLEAR LS_DATA .
  ENDLOOP.


  LV_KUNNR = IM_WERKS .


********************************Sales Order Data Logic**************************************************

  IF LV_GEN IS INITIAL.
    LS_DATA_TMP-MATNR = LS_MEAN-MATNR.
    SELECT * FROM ZVBRP_VRPMA
              INTO TABLE @LT_VBRP
              WHERE MATNR = @LS_MEAN-MATNR
              AND VKORG = '1100'
              AND FKART IN ('ZFP','FP')
              AND KUNNR = @LV_KUNNR
              AND KUNAG = @LV_KUNNR
              AND VBTYP IN ('M','N')
              AND FKTYP = 'W'.

  ELSE.

    CONCATENATE LS_MEAN-MATNR+0(15) '%' INTO LV_MATNR.
    LS_DATA_TMP-MATNR = LS_MEAN-MATNR+0(15).

    SELECT
      A~MATNR AS MATNR ,
      A~VKORG AS VKORG ,
      A~FKDAT AS FKDAT ,
      A~VTWEG AS VTWEG,
      A~FKART AS FKART,
      A~KUNNR AS KUNNR,
      A~KUNAG AS KUNAG,
      A~VBTYP AS VBTYP,
      A~ERNAM AS ERNAM,
      A~VBELN AS VBELN,
      A~POSNR AS POSNR,
      A~FKTYP AS FKTYP,
      A~ADRNR AS ADRNR,
      A~BELNR AS BELNR,
      A~GJAHR AS GJAHR,
*      // b~vbeln AS vbeln
*      // b~posnr AS posnr
      B~UEPOS AS UEPOS,
      B~FKIMG AS FKIMG,
      B~VRKME AS VRKME,
      B~UMVKZ AS UMVKZ,
      B~UMVKN AS UMVKN,
      B~MEINS AS MEINS,
      B~SMENG AS SMENG,
      B~FKLMG AS FKLMG,
      B~LMENG AS LMENG,
      B~NTGEW AS NTGEW,
      B~BRGEW AS BRGEW,
      B~GEWEI AS GEWEI,
      B~VOLUM AS VOLUM,
      B~VOLEH AS VOLEH,
      B~GSBER AS GSBER,
      B~PRSDT AS PRSDT,
      B~FBUDA AS FBUDA,
      B~KURSK AS KURSK,
      B~NETWR AS NETWR,
      B~VBELV AS VBELV,
      B~POSNV AS POSNV,
      B~VGBEL AS VGBEL,
      B~VGPOS AS VGPOS,
      B~VGTYP AS VGTYP,
      B~AUBEL AS AUBEL,
      B~AUPOS AS AUPOS,
      B~AUREF AS AUREF,
*     // b~matnr AS matnr
       B~ARKTX AS ARKTX,
      B~PMATN AS PMATN,
      B~CHARG AS CHARG,
      B~MATKL AS MATKL,
      B~PSTYV AS PSTYV,
      B~POSAR AS POSAR,
      B~PRODH AS PRODH,
      B~VSTEL AS VSTEL,
      B~ATPKZ AS ATPKZ,
      B~SPART AS SPART,
      B~POSPA AS POSPA,
      B~WERKS AS WERKS,
      B~ALAND AS ALAND,
      B~WKREG AS WKREG,
      B~WKCOU AS WKCOU,
      B~WKCTY AS WKCTY,
      B~TAXM1 AS TAXM1,
      B~TAXM2 AS TAXM2,
      B~TAXM3 AS TAXM3,
      B~TAXM4 AS TAXM4,
      B~TAXM5 AS TAXM5,
      B~TAXM6 AS TAXM6,
      B~TAXM7 AS TAXM7,
      B~TAXM8 AS TAXM8,
      B~TAXM9 AS TAXM9,
      B~KOWRR AS KOWRR,
      B~PRSFD AS PRSFD,
      B~SKTOF AS SKTOF,
      B~SKFBP AS SKFBP,
      B~KONDM AS KONDM,
      B~KTGRM AS KTGRM,
      B~KOSTL AS KOSTL,
      B~BONUS AS BONUS,
      B~PROVG AS PROVG,
      B~EANNR AS EANNR,
      B~VKGRP AS VKGRP,
      B~VKBUR AS VKBUR,
      B~SPARA AS SPARA,
      B~SHKZG AS SHKZG,
*     // b~ernam AS ernam,
      B~ERDAT AS ERDAT,
      B~ERZET AS ERZET,
      B~BWTAR AS BWTAR,
      B~LGORT AS LGORT,
      B~STAFO AS STAFO,
      B~WAVWR AS WAVWR,
      B~KZWI1 AS KZWI1,
      B~KZWI2 AS KZWI2,
      B~KZWI3 AS KZWI3,
      B~KZWI4 AS KZWI4,
      B~KZWI5 AS KZWI5,
      B~KZWI6 AS KZWI6,
      B~STCUR AS STCUR,
      B~UVPRS AS UVPRS,
      B~UVALL AS UVALL,
      B~EAN11 AS EAN11,
      B~PRCTR AS PRCTR,
      B~KVGR1 AS KVGR1,
      B~KVGR2 AS KVGR2,
      B~KVGR3 AS KVGR3,
      B~KVGR4 AS KVGR4,
      B~KVGR5 AS KVGR5,
      B~MVGR1 AS MVGR1,
      B~MVGR2 AS MVGR2,
      B~MVGR3 AS MVGR3,
      B~MVGR4 AS MVGR4,
      B~MVGR5 AS MVGR5,
      B~MATWA AS MATWA,
      B~BONBA AS BONBA,
      B~KOKRS AS KOKRS,
      B~PAOBJNR AS PAOBJNR,
      B~PS_PSP_PNR AS PSPSPPNR,
      B~AUFNR AS AUFNR,
      B~TXJCD AS TXJCD,
      B~CMPRE AS CMPRE,
      B~CMPNT AS CMPNT,
      B~CUOBJ AS CUOBJ,
      B~CUOBJ_CH AS CUOBJCH,
      B~KOUPD AS KOUPD,
      B~UECHA AS UECHA,
      B~XCHAR AS XCHAR,
      B~ABRVW AS ABRVW,
      B~SERNR AS SERNR,
      B~BZIRK_AUFT AS BZIRKAUFT,
      B~KDGRP_AUFT AS KDGRPAUFT,
      B~KONDA_AUFT AS KONDAAUFT,
      B~LLAND_AUFT AS LLANDAUFT,
      B~MPROK AS MPROK,
      B~PLTYP_AUFT AS PLTYPAUFT,
      B~REGIO_AUFT AS REGIOAUFT,
      B~VKORG_AUFT AS VKORGAUFT,
      B~VTWEG_AUFT AS VTWEGAUFT,
      B~ABRBG AS ABRBG,
      B~PROSA AS PROSA,
      B~UEPVW AS UEPVW,
      B~AUTYP AS AUTYP,
      B~STADAT AS STADAT,
      B~FPLNR AS FPLNR,
      B~FPLTR AS FPLTR,
      B~AKTNR AS AKTNR,
      B~KNUMA_PI AS KNUMAPI,
      B~KNUMA_AG AS KNUMAAG,
      B~MWSBP AS MWSBP,
      B~AUGRU_AUFT AS AUGRUAUFT,
      B~FAREG AS FAREG,
      B~UPMAT AS UPMAT,
      B~UKONM AS UKONM,
      B~CMPRE_FLT AS CMPREFLT,
      B~ABFOR AS ABFOR,
      B~ABGES AS ABGES,
      B~J_1ARFZ AS J1ARFZ,
      B~J_1AREGIO AS J1AREGIO,
      B~J_1AGICD AS J1AGICD,
      B~J_1ADTYP AS J1ADTYP,
      B~J_1ATXREL AS J1ATXREL,
      B~J_1BCFOP AS J1BCFOP,
      B~J_1BTAXLW1 AS J1BTAXLW1,
      B~J_1BTAXLW2 AS J1BTAXLW2,
      B~J_1BTXSDC AS J1BTXSDC,
      B~BRTWR AS BRTWR,
      B~WKTNR AS WKTNR,
      B~WKTPS AS WKTPS,
      B~RPLNR AS RPLNR,
      B~KURSK_DAT AS KURSKDAT,
      B~WGRU1 AS WGRU1,
      B~WGRU2 AS WGRU2,
      B~KDKG1 AS KDKG1,
      B~KDKG2 AS KDKG2,
      B~KDKG3 AS KDKG3,
      B~KDKG4 AS KDKG4,
      B~KDKG5 AS KDKG5,
      B~VKAUS AS VKAUS,
      B~J_1AINDXP AS J1AINDXP,
      B~J_1AIDATEP AS J1AIDATEP,
      B~KZFME AS KZFME,
      B~MWSKZ AS MWSKZ,
      B~VERTT AS VERTT,
      B~VERTN AS VERTN,
      B~SGTXT AS SGTXT,
      B~DELCO AS DELCO,
      B~BEMOT AS BEMOT,
      B~RRREL AS RRREL,
      B~WMINR AS WMINR,
      B~VGBEL_EX AS VGBELEX,
      B~VGPOS_EX AS VGPOSEX,
      B~LOGSYS AS LOGSYS,
      B~VGTYP_EX AS VGTYPEX,
      B~J_1BTAXLW3 AS J1BTAXLW3,
      B~J_1BTAXLW4 AS J1BTAXLW4,
      B~J_1BTAXLW5 AS J1BTAXLW5,
      B~MSR_ID AS MSRID,
      B~MSR_REFUND_CODE AS MSRREFUNDCODE,
      B~MSR_RET_REASON AS MSRRETREASON,
      B~NRAB_KNUMH AS NRABKNUMH,
      B~NRAB_VALUE AS NRABVALUE,
      B~DISPUTE_CASE AS DISPUTECASE,
      B~FUND_USAGE_ITEM AS FUNDUSAGEITEM,
      B~FARR_RELTYPE AS FARRRELTYPE,
      B~CLAIMS_TAXATION AS CLAIMSTAXATION,
      B~KURRF_DAT_ORIG AS KURRFDATORIG,
      B~SGT_RCAT AS SGTRCAT,
      B~SGT_SCAT AS SGTSCAT,
      B~PREFE AS PREFE,
      B~AKKUR AS AKKUR,
      B~WAERK AS WAERK,
      B~DRAFT AS DRAFT,
      B~ACTIVEDOCUMENT AS ACTIVEDOCUMENT,
      B~GRWRT AS GRWRT,
      B~FKSAA AS FKSAA,
      B~ABSTA AS ABSTA,
      B~ABGRU AS ABGRU,
      B~MWSK1 AS MWSK1,
      B~TXDAT_FROM AS TXDATFROM,
      B~PBD_ID AS PBDID,
      B~PBD_ITEM_ID AS PBDITEMID,
      B~CATS_OVERTIME_CATEGORY AS CATSOVERTIMECATEGORY,
      B~CONTR_DP_SETTL AS CONTRDPSETTL,
      B~PRODH_UNIV_SALES_PARNT_NODID AS PRODHUNIVSALESPARNTNODID,
      B~REASON_CODE AS REASONCODE,
      B~_DATAAGING AS DATAAGING,
      B~SPE_HERKL AS SPEHERKL,
      B~SPE_HERKR AS SPEHERKR,
      B~ITM_COMCO AS ITMCOMCO,
      B~DUMMY_BILLGDOCITEM_INCL_EEW_PS AS DUMMYBILLGDOCITEMINCLEEWPS,
      B~SERVICE_DOC_TYPE AS SERVICEDOCTYPE,
      B~SERVICE_DOC_ID AS SERVICEDOCID,
      B~SERVICE_DOC_ITEM_ID AS SERVICEDOCITEMID,
      B~SOLUTION_ORDER_ID AS SOLUTIONORDERID,
      B~SOLUTION_ORDER_ITEM_ID AS SOLUTIONORDERITEMID,
      B~/CWM/MENGE,
      B~/CWM/MEINS,
      B~VBTYP_ANA AS VBTYPANA,
      B~FKART_ANA AS FKARTANA,
      B~VKORG_ANA AS VKORGANA,
      B~VTWEG_ANA AS VTWEGANA,
      B~KONDA_ANA AS KONDAANA,
      B~KDGRP_ANA AS KDGRPANA,
      B~LAND1_ANA AS LAND1ANA,
      B~REGIO_ANA AS REGIOANA,
      B~CITYC_ANA AS CITYCANA,
      B~BZIRK_ANA AS BZIRKANA,
      B~GBSTK_ANA AS GBSTKANA,
      B~VF_STATUS_ANA AS VFSTATUSANA,
      B~KUNAG_ANA AS KUNAGANA,
      B~KUNRG_ANA AS KUNRGANA,
      B~FKDAT_ANA AS FKDATANA,
      B~BUKRS_ANA AS BUKRSANA,
      B~COUNC_ANA AS COUNCANA,
      B~KNUMA_ANA AS KNUMAANA,
      B~FKTYP_ANA AS FKTYPANA,
      B~KNUMV_ANA AS KNUMVANA,
      B~KUNWE_ANA AS KUNWEANA,
      B~KUNRE_ANA AS KUNREANA,
      B~PERVE_ANA AS PERVEANA,
      B~PERZM_ANA AS PERZMANA,
      B~GLO_LOG_REF1_IT AS GLOLOGREF1IT,
      B~TXS_BUSINESS_TRANSACTION AS TXSBUSINESSTRANSACTION,
      B~TXS_MATERIAL_USAGE AS TXSMATERIALUSAGE,
      B~TXS_USAGE_PURPOSE AS TXSUSAGEPURPOSE,
      B~ZAPCGKI AS ZAPCGKI,
      B~APCGK_EXTENDI AS APCGKEXTENDI,
      B~ZABDATI AS ZABDATI,
      B~AUFPL AS AUFPL,
      B~APLZL AS APLZL,
      B~DPCNR AS DPCNR,
      B~DCPNR AS DCPNR,
      B~DPNRB AS DPNRB,
      B~BOSFAR AS BOSFAR,
      B~DP_BELNR AS DPBELNR,
      B~DP_BUKRS AS DPBUKRS,
      B~DP_GJAHR AS DPGJAHR,
      B~DP_BUZEI AS DPBUZEI,
      B~PACKNO AS PACKNO,
      B~PEROP_BEG AS PEROPBEG,
      B~PEROP_END AS PEROPEND,
      B~FMFGUS_KEY AS FMFGUSKEY,
      B~FSH_SEASON_YEAR AS FSHSEASONYEAR,
      B~FSH_SEASON AS FSHSEASON,
      B~FSH_COLLECTION AS FSHCOLLECTION,
      B~FSH_THEME AS FSHTHEME,
      B~FONDS AS FONDS,
      B~FISTL AS FISTL,
      B~FKBER AS FKBER,
      B~GRANT_NBR AS GRANTNBR,
      B~BUDGET_PD AS BUDGETPD,
      B~J_3GBELNRI AS J3GBELNRI,
      B~J_3GPMAUFE AS J3GPMAUFE,
      B~J_3GPMAUFV AS J3GPMAUFV,
      B~J_3GETYPA AS J3GETYPA,
      B~J_3GETYPE AS J3GETYPE,
      B~J_3GORGUEB AS J3GORGUEB,
      B~PRS_WORK_PERIOD AS PRSWORKPERIOD,
      B~PPRCTR AS PPRCTR,
      B~PARGB AS PARGB,
      B~AUFPL_OAA AS AUFPLOAA,
      B~APLZL_OAA AS APLZLOAA,
      B~CAMPAIGN AS CAMPAIGN,
      B~COMPREAS AS COMPREAS,
      B~WRF_CHARSTC1 AS WRFCHARSTC1,
      B~WRF_CHARSTC2 AS WRFCHARSTC2,
      B~WRF_CHARSTC3 AS WRFCHARSTC3
      FROM VRPMA AS A
   INNER JOIN VBRP AS B ON A~VBELN = B~VBELN AND A~POSNR = B~POSNR
    INTO TABLE  @LT_VBRP
    WHERE A~MATNR LIKE @LV_MATNR
    AND VKORG = '1100'
    AND FKART IN ('ZFP','FP')
    AND KUNNR = @LV_KUNNR
    AND KUNAG = @LV_KUNNR
    AND VBTYP IN ('M','N')
    AND FKTYP = 'W'
    .

*    select * from vrpma
*    into corresponding fields of table @it_vbrp
*    where matnr like @lv_matnr
*    and vkorg = '1100'
*    and fkart in ('ZFP','FP')
*    and kunnr = @lv_kunnr
*    AND kunag = @lv_kunnr
*    AND vbtyp IN ('M','N')
*    AND fktyp = 'W'.

*    LOOP AT it_vbrp INTO wa_vbrp.
*      ls_vbrp-matnr = wa_vbrp-matnr.
*      ls_vbrp-vkorg = wa_vbrp-vkorg.
*      ls_vbrp-fkart = wa_vbrp-fkart.
*      ls_vbrp-kunnr = wa_vbrp-kunnr.
*      ls_vbrp-kunag = wa_vbrp-kunag.
*      ls_vbrp-vbtyp = wa_vbrp-vbtyp.
*      ls_vbrp-fktyp = wa_vbrp-fktyp.
*
*      APPEND ls_vbrp TO lt_vbrp.
*      CLEAR wa_vbrp.
*    ENDLOOP.

    IF LT_VBRP IS NOT INITIAL.
      SELECT * FROM MAKT INTO TABLE LT_MAKT
                      FOR ALL ENTRIES IN LT_VBRP
                        WHERE MATNR = LT_VBRP-MATNR
                        AND SPRAS = SY-LANGU.
    ENDIF.
  ENDIF.

  LV_PREV_DATE = SY-DATUM - 1.

*  IF sy-datum+6(2) = '01' .

  CALL FUNCTION 'OIL_GET_PREV_MONTH'
    EXPORTING
      I_DATE = SY-DATUM
    IMPORTING
      E_DATE = LV_PRE_M_DT.

  CALL FUNCTION 'RP_LAST_DAY_OF_MONTHS'
    EXPORTING
      DAY_IN            = LV_PRE_M_DT
    IMPORTING
      LAST_DAY_OF_MONTH = LV_PRE_M_LT
    EXCEPTIONS
      DAY_IN_NO_DATE    = 1
      OTHERS            = 2.
  IF SY-SUBRC <> 0.
    MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
            WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.
  ENDIF.

*  ENDIF.

  LV_PRE_M_FIR_DT = LV_PRE_M_DT .
  LV_PRE_M_FIR_DT+6(2) = '01' .
  SORT LT_VBRP BY FKDAT MATNR.
  LOOP AT LT_VBRP INTO LS_VBRP.

    CLEAR LS_MAKT.
    READ TABLE LT_MAKT INTO LS_MAKT WITH KEY MATNR = LS_VBRP-MATNR
                                            BINARY SEARCH .
    IF SY-SUBRC IS INITIAL .
      LS_DATA_TMP-MAKTX = LS_MAKT-MAKTX.
    ENDIF.

    SELECT SINGLE * FROM MARA INTO LS_MARA WHERE MATNR = LS_VBRP-MATNR .
    IF SY-SUBRC IS INITIAL .

      LS_DATA_TMP-SIZE1 = LS_MARA-SIZE1.
      LS_DATA_TMP-COLOR = LS_MARA-COLOR.
    ENDIF.

*    ls_data-endpr = ls_vbrp-endpr.    " MRp
*    ls_data-matnr = ls_vbrp-endpr.    " MRp
    LS_DATA_TMP-MATNR = LS_VBRP-MATNR .
    IF LS_VBRP-NETWR LT 0 .
      LS_VBRP-FKIMG = LS_VBRP-FKIMG * -1.
*      ls_vbrp-kzwi1 = ls_vbrp-kzwi1 * -1.
*      ls_vbrp-kzwi4 = ls_vbrp-kzwi4 * -1.
*      ls_vbrp-mwsbp = ls_vbrp-mwsbp * -1.
    ENDIF.

    IF LS_VBRP-FKDAT BETWEEN LV_PREV_DATE AND SY-DATUM.
* ls_Data-WAVWR1 =  ls_vbrp-WAVWR1 + ( ls_vbrp-fkimg * ls_vbrp-WAVWR ).    " MAP
      LS_DATA_TMP-TD_QTY =  LS_DATA_TMP-TD_QTY + LS_VBRP-FKIMG.
      LS_DATA_TMP-TD_NVAL =  LS_DATA_TMP-TD_NVAL + LS_VBRP-KZWI1.   "  gross value
*    ls_data-td_gdisc =  ls_data-td_gdisc + ls_vbrp-kzwi2. " gross disc
*    ls_data-td_gdnet =  ls_data-td_gdnet + ls_vbrp-kzwi4. " net value
*    ls_data-td_mwsbp =  ls_data-td_mwsbp + ls_vbrp-mwsbp. " tax value
    ENDIF.

    CLEAR LV_MONTH.
    LV_MONTH = LS_VBRP-FKDAT+4(2).

    IF LV_MONTH BETWEEN 01 AND 03.
      E_FY = LS_VBRP-FKDAT+0(4) - 1 .
*      e_fy = lv_year .
    ELSE.
      E_FY = LS_VBRP-FKDAT+0(4).
    ENDIF.
*    CALL FUNCTION 'GM_GET_FISCAL_YEAR'
*      EXPORTING
*       i_date                           = ls_vbrp-fkdat
*        i_fyv                            = 'V3'
*     IMPORTING
*       e_fy                             = e_fy
** EXCEPTIONS
**   FISCAL_YEAR_DOES_NOT_EXIST       = 1
**   NOT_DEFINED_FOR_DATE             = 2
**   OTHERS                           = 3
*              .
*    IF sy-subrc <> 0.
** MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
**         WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.
*    ENDIF.

    IF E_FY = SY-DATUM+0(4).
* ls_Data-WAVWR1 =  ls_vbrp-WAVWR1 + ( ls_vbrp-fkimg * ls_vbrp-WAVWR ).    " MAP
      LS_DATA_TMP-YTD_QTY =  LS_DATA_TMP-YTD_QTY + LS_VBRP-FKIMG.
      LS_DATA_TMP-YTD_NVAL =  LS_DATA_TMP-YTD_NVAL + LS_VBRP-KZWI1.   "  gross value
*      ls_data-ytd_gdisc =  ls_data-ytd_gdisc + ls_vbrp-kzwi2. " gross disc
*      ls_data-ytd_gdnet =  ls_data-ytd_gdnet + ls_vbrp-kzwi4. " net value
*      ls_data-ytd_mwsbp =  ls_data-ytd_mwsbp + ls_vbrp-mwsbp. " tax value
    ENDIF.

    IF SY-DATUM+0(6) = LS_VBRP-FKDAT+0(6).
* ls_Data-WAVWR1 =  ls_vbrp-WAVWR1 + ( ls_vbrp-fkimg * ls_vbrp-WAVWR ).    " MAP
      LS_DATA_TMP-MTD_QTY =  LS_DATA_TMP-MTD_QTY + LS_VBRP-FKIMG.
      LS_DATA_TMP-MTD_NVAL =  LS_DATA_TMP-MTD_NVAL + LS_VBRP-KZWI1.   "  gross value
*      ls_data-mtd_gdisc =  ls_data-mtd_gdisc + ls_vbrp-kzwi2. " gross disc
*      ls_data-mtd_gdnet =  ls_data-mtd_gdnet + ls_vbrp-kzwi4. " net value
*      ls_data-mtd_mwsbp =  ls_data-mtd_mwsbp + ls_vbrp-mwsbp. " tax value
    ENDIF.

    IF SY-DATUM+6(2) = '01'.

      IF LS_VBRP-FKDAT BETWEEN LV_PRE_M_DT AND LV_PRE_M_LT.
* ls_Data-WAVWR1 =  ls_vbrp-WAVWR1 + ( ls_vbrp-fkimg * ls_vbrp-WAVWR ).    " MAP
        LS_DATA_TMP-MTD_QTY =  LS_DATA_TMP-MTD_QTY + LS_VBRP-FKIMG.
        LS_DATA_TMP-MTD_NVAL =  LS_DATA_TMP-MTD_NVAL + LS_VBRP-KZWI1.   "  gross value
*      ls_data-mtd_gdisc =  ls_data-mtd_gdisc + ls_vbrp-kzwi2. " gross disc
*      ls_data-mtd_gdnet =  ls_data-mtd_gdnet + ls_vbrp-kzwi4. " net value
*      ls_data-mtd_mwsbp =  ls_data-mtd_mwsbp + ls_vbrp-mwsbp. " tax value
      ENDIF.
    ENDIF.



    IF LS_VBRP-FKDAT BETWEEN LV_PRE_M_FIR_DT AND LV_PRE_M_LT.
* ls_Data-WAVWR1 =  ls_vbrp-WAVWR1 + ( ls_vbrp-fkimg * ls_vbrp-WAVWR ).    " MAP
      LS_DATA_TMP-LMTD_QTY =  LS_DATA-LMTD_QTY + LS_VBRP-FKIMG .
      LS_DATA_TMP-LMTD_NVAL =  LS_DATA-LMTD_NVAL + LS_VBRP-KZWI1.   "  gross value
*      ls_data-mtd_gdisc =  ls_data-mtd_gdisc + ls_vbrp-kzwi2. " gross disc
*      ls_data-mtd_gdnet =  ls_data-mtd_gdnet + ls_vbrp-kzwi4. " net value
*      ls_data-mtd_mwsbp =  ls_data-mtd_mwsbp + ls_vbrp-mwsbp. " tax value
    ENDIF.

    COLLECT LS_DATA_TMP INTO LT_DATA_TMP .
    CLEAR LS_DATA_TMP .
    CLEAR LS_VBRP.
  ENDLOOP.

  SORT IT_MSEG4 BY BLDAT DESCENDING.
  IF ET_DATA[] IS NOT INITIAL.
    LOOP AT ET_DATA INTO LS_DATA.
*
*      if ls_data-v01 is initial and
*         ls_data-v04 is initial and
*         ls_data-msa is initial and
*         ls_data-0001 is initial and
*         ls_data-0003 is initial and
*         ls_data-0004 is initial and
*         ls_data-0005 is initial and
*         ls_data-0006 is initial and
*         ls_data-0007 is initial and
*         ls_data-0008 is initial and
*         ls_data-0009 is initial and
*         ls_data-0010 is initial.
*
*
*        clear ls_data.
*
*      endif.
*      READ TABLE it_mseg4 INTO ls_mseg4 WITH KEY matnr = ls_data-matnr .
*      IF sy-subrc = 0.
*        CALL FUNCTION 'DAYS_BETWEEN_TWO_DATES'
*          EXPORTING
*            i_datum_bis                   = sy-datum
*            i_datum_von                   = ls_mseg4-bldat
*
*         IMPORTING
*           e_tage                        = ls_data-no_days
**   EXCEPTIONS
**     DAYS_METHOD_NOT_DEFINED       = 1
**     OTHERS                        = 2
*                  .
*        IF sy-subrc <> 0.
** MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
**         WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.
*        ENDIF.
*
*      ENDIF.
*      MODIFY et_data FROM ls_data.

        SELECT SINGLE MTVER FROM ZZMARC INTO @DATA(LV_MTVER1) WHERE MATNR = @LS_DATA-MATNR AND WERKS = @IM_WERKS.
        IF SY-SUBRC IS INITIAL .
          LS_DATA-MATKL = LV_MTVER1.

        ENDIF.

        READ TABLE LT_DATA_TMP INTO LS_DATA_TMP WITH KEY MATNR = LS_DATA-MATNR.
        IF SY-SUBRC = 0.
          LS_DATA-ENDPR        = LS_DATA_TMP-ENDPR.
          LS_DATA-TD_QTY       = LS_DATA_TMP-TD_QTY.
          LS_DATA-TD_NVAL      = LS_DATA_TMP-TD_NVAL.
          LS_DATA-YTD_QTY      = LS_DATA_TMP-YTD_QTY.
          LS_DATA-YTD_NVAL     = LS_DATA_TMP-YTD_NVAL.
          LS_DATA-MTD_QTY      = LS_DATA_TMP-MTD_QTY.
          LS_DATA-MTD_NVAL     = LS_DATA_TMP-MTD_NVAL.
          LS_DATA-LMTD_QTY      = LS_DATA_TMP-LMTD_QTY.
          LS_DATA-LMTD_NVAL     = LS_DATA_TMP-LMTD_NVAL.
          MODIFY ET_DATA FROM LS_DATA.
          DELETE LT_DATA_TMP WHERE MATNR = LS_DATA-MATNR.
        ELSE.
          MODIFY ET_DATA FROM LS_DATA.
        ENDIF.
        CLEAR: LS_DATA,LS_DATA_TMP.
      ENDLOOP.

    ENDIF.


    IF LT_DATA_TMP[] IS NOT INITIAL.

      SELECT A~MATNR,
             A~WERKS,
             A~IROD
       FROM ZZMARC AS A
      INNER JOIN @LT_DATA_TMP AS B
         ON A~MATNR EQ B~MATNR
        AND A~WERKS EQ @IM_WERKS
       INTO TABLE @LT_ZMARC.

        SORT LT_ZMARC BY MATNR WERKS.
        DELETE ADJACENT DUPLICATES FROM LT_ZMARC.


        LOOP AT LT_DATA_TMP INTO LS_DATA_TMP .

          MOVE-CORRESPONDING LS_DATA_TMP TO LS_DATA.


          READ TABLE LT_ZMARC ASSIGNING <LFS_ZMARC>
           WITH KEY MATNR = LS_DATA-MATNR
                    WERKS = IM_WERKS
                BINARY SEARCH.

          IF <LFS_ZMARC> IS ASSIGNED.
            LS_DATA-IRODE = <LFS_ZMARC>-IROD.
            LS_DATA-MATKL = <LFS_ZMARC>-MTVER.
          ENDIF.
          UNASSIGN <LFS_ZMARC>.

**      select single mtver from marc into @data(lv_mtver) where matnr = @ls_data-matnr and werks = @im_werks.
*      select single mtver from zzmarc into @data(lv_mtver) where matnr = @ls_data-matnr and werks = @im_werks.
*      if sy-subrc is initial .
**        ls_data-matkl = lv_mtver.
*
*
          APPEND LS_DATA TO ET_DATA.



          CLEAR: LS_DATA,LS_DATA_TMP,LS_MSEG4.

        ENDLOOP.
      ENDIF.

      DATA : LT_DATA_SUM TYPE TABLE OF ZWM_ST_SA_DATA_RETAIL_APP.
*  DATA(lt_data_t) = et_data[].
      MOVE-CORRESPONDING ET_DATA[] TO LT_DATA_SUM[].

      SORT LT_DATA_SUM BY COLOR.
**  if lines( lt_data_sum ) gt 0.
      SELECT * FROM ZRETAIL_APP_UPLD  INTO TABLE @DATA(LT_RETAIL)
                            FOR ALL ENTRIES IN @LT_DATA_SUM
                            WHERE ST_CD EQ @LT_DATA_SUM-WERKS
                                  AND GENART EQ @LT_DATA_SUM-SATNR
                                  AND COLOR EQ @LT_DATA_SUM-COLOR.
**  endif.
        LOOP AT LT_DATA_SUM INTO DATA(LS_DATA_SUM).
          DATA(LS_DATA_S) = LS_DATA_SUM.
          AT NEW COLOR.
            APPEND INITIAL LINE TO ET_DATA_SUM ASSIGNING FIELD-SYMBOL(<LFS_DATA>).
            <LFS_DATA>-LGNUM = LS_DATA_S-LGNUM.
            SHIFT LS_DATA_S-MATNR LEFT DELETING LEADING '0'.
            <LFS_DATA>-MATNR = LS_DATA_S-MATNR(10).
            <LFS_DATA>-MAKTX = LS_DATA_S-MAKTX.
            <LFS_DATA>-MATKL = LS_DATA_S-MATKL.
            <LFS_DATA>-WERKS = LS_DATA_S-WERKS.
            <LFS_DATA>-EAN11 = LS_DATA_S-EAN11.
            <LFS_DATA>-COMP = LS_DATA_S-COMP.
            <LFS_DATA>-INTRANSIT = LS_DATA_S-INTRANSIT.
            <LFS_DATA>-SALE_AGEING = LS_DATA_S-SALE_AGEING.
            <LFS_DATA>-STOCK_AGEING = LS_DATA_S-STOCK_AGEING.
            <LFS_DATA>-SIZE1 = LS_DATA_S-SIZE1.
            <LFS_DATA>-IRODE = LS_DATA_S-IRODE.
            <LFS_DATA>-COLOR = LS_DATA_S-COLOR.
            <LFS_DATA>-SATNR = LS_DATA_S-SATNR."VALUE #( lt_genrice[ matnr = <lfs_data>-matnr ]-satnr OPTIONAL ).
            DATA(LS_RETAIL) = VALUE #( LT_RETAIL[ ST_CD  = <LFS_DATA>-WERKS
                                                  GENART = <LFS_DATA>-SATNR
                                                  COLOR = <LFS_DATA>-COLOR   ] OPTIONAL ) .
            <LFS_DATA>-STR_MTD =  LS_RETAIL-STR_MTD.
            <LFS_DATA>-STR_L7 =  LS_RETAIL-STR_L7.
            <LFS_DATA>-STR_L30 =  LS_RETAIL-STR_L30.
            <LFS_DATA>-SALE_PSF_MTD =  LS_RETAIL-SALE_PSF_MTD.
            <LFS_DATA>-SALE_PSF_L7 =  LS_RETAIL-SALE_PSF_L7.
            <LFS_DATA>-SALE_PSF_L30 =  LS_RETAIL-SALE_PSF_L30.
            <LFS_DATA>-GP_PSF_MTD =  LS_RETAIL-GP_PSF_MTD.
            <LFS_DATA>-GP_PSF_L7 =  LS_RETAIL-GP_PSF_L7.
            <LFS_DATA>-GP_PSF_L30 =  LS_RETAIL-GP_PSF_L30.
          ENDAT.

          <LFS_DATA>-V01 = <LFS_DATA>-V01 + LS_DATA_S-V01.
          <LFS_DATA>-V04 = <LFS_DATA>-V04 + LS_DATA_S-V04.
          <LFS_DATA>-V02 = <LFS_DATA>-V02 + LS_DATA_S-V02.
          <LFS_DATA>-0001 = <LFS_DATA>-0001 + LS_DATA_S-0001.
          <LFS_DATA>-0003 = <LFS_DATA>-0003 + LS_DATA_S-0003.
          <LFS_DATA>-0004 = <LFS_DATA>-0004 + LS_DATA_S-0004.
          <LFS_DATA>-0005 = <LFS_DATA>-0005 + LS_DATA_S-0005.
          <LFS_DATA>-0006 = <LFS_DATA>-0006 + LS_DATA_S-0006.
          <LFS_DATA>-0007 = <LFS_DATA>-0007 + LS_DATA_S-0007.
          <LFS_DATA>-0008 = <LFS_DATA>-0008 + LS_DATA_S-0008.
          <LFS_DATA>-0009 = <LFS_DATA>-0009 + LS_DATA_S-0009.
          <LFS_DATA>-0010 = <LFS_DATA>-0010 + LS_DATA_S-0010.
          <LFS_DATA>-ENDPR = <LFS_DATA>-ENDPR + LS_DATA_S-ENDPR.
          <LFS_DATA>-TD_QTY = <LFS_DATA>-TD_QTY + LS_DATA_S-TD_QTY.
          <LFS_DATA>-TD_NVAL = <LFS_DATA>-TD_NVAL + LS_DATA_S-TD_NVAL.
          <LFS_DATA>-YTD_QTY = <LFS_DATA>-YTD_QTY + LS_DATA_S-YTD_QTY.
          <LFS_DATA>-YTD_NVAL = <LFS_DATA>-YTD_NVAL + LS_DATA_S-YTD_NVAL.
          <LFS_DATA>-MTD_QTY = <LFS_DATA>-MTD_QTY + LS_DATA_S-MTD_QTY.
          <LFS_DATA>-MTD_NVAL = <LFS_DATA>-MTD_NVAL + LS_DATA_S-MTD_NVAL.
          <LFS_DATA>-LMTD_QTY = <LFS_DATA>-LMTD_QTY + LS_DATA_S-LMTD_QTY.
          <LFS_DATA>-LMTD_NVAL = <LFS_DATA>-LMTD_NVAL + LS_DATA_S-LMTD_NVAL.
          <LFS_DATA>-NO_DAYS = <LFS_DATA>-NO_DAYS + LS_DATA_S-NO_DAYS.
          CLEAR LS_RETAIL.
        ENDLOOP.

      ENDFUNCTION.