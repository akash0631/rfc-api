FUNCTION ZSDC_V11_TAKE_SAVE_V11.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_LGNUM) TYPE  LGNUM OPTIONAL
*"     VALUE(IM_NLTYP) TYPE  LGTYP OPTIONAL
*"     VALUE(IM_BIN_VAL) TYPE  LGPLA OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA TYPE  ZWM_BIN_SCAN_T OPTIONAL
*"----------------------------------------------------------------------

  DATA: LV_SITE  TYPE WERKS_D,
        LV_NLTYP TYPE LGTYP,
        LV_LGNUM TYPE LGNUM,
        LV_VLPLA TYPE LGPLA,
        LV_TANUM TYPE TANUM,
        LV_NLPLA TYPE LGPLA,
        LV_CRATE TYPE ZZCRATE,
        LV_VLTYP TYPE LGTYP.

  DATA: LV_MSG(100) TYPE C,
       P_NUMBER TYPE NUMC10.

  DATA: LT_LTAP_CREATE TYPE STANDARD TABLE OF LTAP_CREAT INITIAL SIZE 0,
        LT_LTAP_VB     TYPE STANDARD TABLE OF LTAP_VB,
        LS_LTAP_CREATE TYPE LTAP_CREAT,
        LT_CRATE       TYPE TABLE OF ZWM_CRATE,
        LT_CRATE1      TYPE TABLE OF ZWM_CRATE,
        LS_CRATE       TYPE ZWM_CRATE,
        LT_LAGP        TYPE TABLE OF LAGP,
        LS_LAGP        TYPE LAGP.

  DATA : LT_HST  TYPE STANDARD TABLE OF ZWM_STORE_HST,
         LS_HST  TYPE ZWM_STORE_HST.
         DATA : L_POSNR  TYPE POSNR.

  FIELD-SYMBOLS: <LFS_DATA> TYPE ZWM_BIN_SCAN.
    CLEAR: L_POSNR.
  IF IT_DATA[] IS INITIAL.
    EX_RETURN-MESSAGE = 'No Data for Process'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.

  LV_NLTYP = IM_NLTYP. " 'D22'
  LV_VLTYP = 'V11'.
  LV_VLPLA = 'PICKI-CON'. "Destination Bin

  DATA:LV_MATNR(18) TYPE C.

  LOOP AT IT_DATA ASSIGNING  <LFS_DATA>.
    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        INPUT  = <LFS_DATA>-MATERIAL
      IMPORTING
        OUTPUT = <LFS_DATA>-MATERIAL.

    LV_MATNR = <LFS_DATA>-MATERIAL+22(18).
    <LFS_DATA>-MATERIAL = LV_MATNR.

    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        INPUT  = <LFS_DATA>-SLOC
      IMPORTING
        OUTPUT = <LFS_DATA>-SLOC.

    TRANSLATE <LFS_DATA>-BIN TO UPPER CASE.
    LV_LGNUM = 'SDC'. "IM_lgnum. "'V2R'.

    LS_LTAP_CREATE-MATNR = <LFS_DATA>-MATERIAL .
    LS_LTAP_CREATE-WERKS = <LFS_DATA>-SITE .
    LS_LTAP_CREATE-LGORT =  '0002'. "<lfs_data>-sloc .
    LS_LTAP_CREATE-ANFME = <LFS_DATA>-SCAN_QTY.
    LS_LTAP_CREATE-ALTME =  'EA'.
    LS_LTAP_CREATE-SQUIT =  'X'.
    LS_LTAP_CREATE-VLTYP = LV_VLTYP.
    LS_LTAP_CREATE-VLPLA = LV_VLPLA.  " --Bhatat on 10.06.2022
    LS_LTAP_CREATE-VLBER = '001'.
    LS_LTAP_CREATE-NLTYP = LV_NLTYP.
    LS_LTAP_CREATE-NLPLA = <LFS_DATA>-BIN.
    LS_LTAP_CREATE-NLBER = '001'.
    LV_NLPLA = <LFS_DATA>-BIN.
    LV_CRATE = <LFS_DATA>-CRATE.

    APPEND LS_LTAP_CREATE TO LT_LTAP_CREATE.
    CLEAR: LS_LTAP_CREATE.
  ENDLOOP.

*Duplicate data Save checking within 45 Sec - VKS_11.08.2020
  DATA: LS_LTAP TYPE LTAP,
        LT_LTAP TYPE TABLE OF LTAP,
        LV_TIME.

  RANGES: R_UZEIT FOR SY-UZEIT.

  LV_TIME = SY-UZEIT - 45.

  R_UZEIT-SIGN = 'I'.
  R_UZEIT-OPTION = 'BT'.
  R_UZEIT-LOW = LV_TIME.
  R_UZEIT-HIGH = SY-UZEIT.
  APPEND R_UZEIT TO R_UZEIT.

  SELECT lgnum tanum tapos matnr werks nltyp nlpla vsolm meins
    FROM LTAP INTO TABLE LT_LTAP
    FOR ALL ENTRIES IN LT_LTAP_CREATE
    WHERE LGNUM = LV_LGNUM "'V2R'
      AND VLTYP = 'SDC'  "'V11'
      AND NLTYP = LV_NLTYP "'E01'
      AND MATNR = LT_LTAP_CREATE-MATNR
      AND WERKS = LT_LTAP_CREATE-WERKS
      AND LGORT = LT_LTAP_CREATE-LGORT
      AND NLPLA = LT_LTAP_CREATE-NLPLA
      AND VSOLM = LT_LTAP_CREATE-ANFME
      AND QDATU = SY-DATUM
      AND QZEIT IN R_UZEIT
      %_HINTS ORACLE 'INDEX("LTAP" "LTAP~ZMN")'.
  IF LT_LTAP IS NOT INITIAL.
    EX_RETURN-MESSAGE = 'Duplicate Record'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.
*End code

  IF LT_LTAP_CREATE[] IS NOT INITIAL.
    CALL FUNCTION 'L_TO_CREATE_MULTIPLE' DESTINATION 'NONE'
      EXPORTING
        I_LGNUM                = LV_LGNUM
        I_BWLVS                = '999'
        I_COMMIT_WORK          = 'X'
        I_KOMPL                = ''
      IMPORTING
        E_TANUM                = LV_TANUM
      TABLES
        T_LTAP_CREAT           = LT_LTAP_CREATE
        T_LTAP_VB              = LT_LTAP_VB
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
*     MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
*             WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.

      CALL FUNCTION 'FORMAT_MESSAGE'
       EXPORTING
         ID              = SY-MSGID
         LANG            = 'E' "'-D'
         NO              = SY-MSGNO
         V1              = SY-MSGV1
         V2              = SY-MSGV2
         V3              = SY-MSGV3
         V4              = SY-MSGV4
       IMPORTING
         MSG             = LV_MSG
       EXCEPTIONS
         NOT_FOUND       = 1
         OTHERS          = 2
                .
      IF SY-SUBRC <> 0.
* Implement suitable error handling here
      ENDIF.
    ENDIF.

          CALL FUNCTION 'NUMBER_GET_NEXT'
    EXPORTING
      NR_RANGE_NR             = '01'
      OBJECT                  = 'ZWM_HST'
*     QUANTITY                = '1'
*     SUBOBJECT               = ' '
*     TOYEAR                  = '0000'
*     IGNORE_BUFFER           = ' '
    IMPORTING
      NUMBER                  = P_NUMBER
*     QUANTITY                =
*     RETURNCODE              =
    EXCEPTIONS
      INTERVAL_NOT_FOUND      = 1
      NUMBER_RANGE_NOT_INTERN = 2
      OBJECT_NOT_FOUND        = 3
      QUANTITY_IS_0           = 4
      QUANTITY_IS_NOT_1       = 5
      INTERVAL_OVERFLOW       = 6
      BUFFER_OVERFLOW         = 7
      OTHERS                  = 8.

  IF SY-SUBRC <> 0.
    MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
            WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.
  ENDIF.

    IF LV_TANUM IS INITIAL.

** update log table
  LOOP AT LT_LTAP_CREATE INTO LS_LTAP_CREATE.
      L_POSNR         = L_POSNR + 1 .
      LS_HST-LGNUM    = 'SDC'.
      LS_HST-HST_NR   = P_NUMBER.
      LS_HST-POSNR    = L_POSNR.
      LS_HST-MATNR    = LS_LTAP_CREATE-MATNR.
      LS_HST-WERKS    = LS_LTAP_CREATE-WERKS.
      LS_HST-SLOC     = '0002'.
      LS_HST-DLOC     = '0002'.
      LS_HST-DOC_TYPE = 'D'.
      LS_HST-NLPLA    = 'PICKI-CON'.
      LS_HST-ANFME    = LS_LTAP_CREATE-ANFME.
      LS_HST-TANUM    = LV_TANUM.
*    READ TABLE pit_lqua INTO ls_lqua WITH KEY
*                              matnr = wa_status-matnr.
*    IF sy-subrc IS INITIAL.
      LS_HST-VLPLA    = LS_LTAP_CREATE-NLPLA. "p_bin.
      LS_HST-MSG      = LV_MSG.  "'TO not created'.
*    ENDIF.
      LS_HST-NLTYP    = 'V11'.
      LS_HST-VLTYP    = LS_LTAP_CREATE-WERKS+1(3).
*      ls_hst-picnr    = p_picnr.
      LS_HST-VLBER    = '001'.
      LS_HST-NLBER    = '001'.
*      ls_hst-anfme    = wa_status-req_0008.
      LS_HST-ALTME    = 'EA'.
      LS_HST-ERNAM    = SY-UNAME.
      LS_HST-ERDAT    = SY-DATUM.
      LS_HST-ERZET    = SY-UZEIT.
      LS_HST-TCODE    = SY-TCODE.

      APPEND LS_HST TO LT_HST .
      CLEAR LS_HST .
  ENDLOOP.
    MODIFY ZWM_STORE_HST FROM TABLE LT_HST.
* end update log table
      EX_RETURN-MESSAGE = LV_MSG. "'TO not created'.
      EX_RETURN-TYPE = 'E'.
      RETURN.
    ELSE.

* comment start by bharat on 11.06.2022
*      select *
*        from zwm_crate
*        into table lt_crate
*        where lgnum = lv_lgnum
*          and crate = lv_crate.
*
*      lt_crate1[] = lt_crate.
*
*      loop at lt_crate into ls_crate.
*        ls_crate-lgpla = lv_nlpla.
*        ls_crate-lgtyp = lv_nltyp.
*        ls_crate-aedat = sy-datum.
*        ls_crate-msa_empty = ''.
*        ls_crate-flr_empty = ''.
*
*        modify lt_crate from ls_crate.
*        clear: ls_crate.
*      endloop.
*
*      delete zwm_crate from table lt_crate1.
*      insert zwm_crate from table lt_crate.
*      commit work and wait.

** comment end by bharat on 11.06.2022

*Update Blank Indicators
      SELECT * FROM LAGP
        INTO TABLE LT_LAGP
        WHERE BTANR = LV_TANUM
          AND LGTYP = LV_NLTYP  "'E01'
          AND LGNUM = LV_LGNUM.

      LOOP AT LT_LAGP INTO LS_LAGP.
        LS_LAGP-LZONE = ''.
        LS_LAGP-KZLER = ''.
*        if ls_lagp-lgnum = 'V2R' and ls_lagp-lgtyp = I'E01'.
        IF LS_LAGP-LGNUM = LV_LGNUM AND LS_LAGP-LGTYP = LV_NLTYP.  "'E01'.
          LS_LAGP-UNAME = SY-UNAME.
          LS_LAGP-LAEDT = SY-DATUM.
          LS_LAGP-KOBER = 'F01'.
        ENDIF.
        MODIFY LT_LAGP FROM LS_LAGP.
        CLEAR:LS_LAGP.
      ENDLOOP.

      UPDATE LAGP FROM TABLE LT_LAGP.
      COMMIT WORK AND WAIT .
** update log table
  LOOP AT LT_LTAP_CREATE INTO LS_LTAP_CREATE.
      L_POSNR         = L_POSNR + 1 .
      LS_HST-LGNUM    = 'SDC'.
      LS_HST-HST_NR   = P_NUMBER.
      LS_HST-POSNR    = L_POSNR.
      LS_HST-MATNR    = LS_LTAP_CREATE-MATNR.
      LS_HST-WERKS    = LS_LTAP_CREATE-WERKS.
      LS_HST-SLOC     = '0002'.
      LS_HST-DLOC     = '0002'.
      LS_HST-DOC_TYPE = 'D'.
      LS_HST-NLPLA    = 'PICKI-CON'.
      LS_HST-ANFME    = LS_LTAP_CREATE-ANFME.
      LS_HST-TANUM    = LV_TANUM.
*      ls_hst-msg     = ex_return-message.
*    READ TABLE pit_lqua INTO ls_lqua WITH KEY
*                              matnr = wa_status-matnr.
*    IF sy-subrc IS INITIAL.
      LS_HST-VLPLA    = LS_LTAP_CREATE-NLPLA. "p_bin.
*    ENDIF.
      LS_HST-NLTYP    = 'V11'.
      LS_HST-VLTYP    = LS_LTAP_CREATE-WERKS+1(3).
*      ls_hst-picnr    = p_picnr.
      LS_HST-VLBER    = '001'.
      LS_HST-NLBER    = '001'.
*      ls_hst-anfme    = wa_status-req_0008.
      LS_HST-ALTME    = 'EA'.
      LS_HST-MBLNR    = '9999999999'.
      LS_HST-MJAHR    = '9999'.
      LS_HST-ERNAM    = SY-UNAME.
      LS_HST-ERDAT    = SY-DATUM.
      LS_HST-ERZET    = SY-UZEIT.
      LS_HST-TCODE    = SY-TCODE.

      APPEND LS_HST TO LT_HST .
      CLEAR LS_HST .
  ENDLOOP.
    MODIFY ZWM_STORE_HST FROM TABLE LT_HST.
** end LOG update
     CONCATENATE 'To' LV_TANUM 'created' INTO EX_RETURN-MESSAGE SEPARATED BY SPACE.
      EX_RETURN-TYPE = 'S'.
      RETURN.
    ENDIF.
  ENDIF.

  REFRESH IT_DATA.
ENDFUNCTION.