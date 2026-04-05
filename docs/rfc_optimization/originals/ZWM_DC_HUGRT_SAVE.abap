FUNCTION ZWM_DC_HUGRT_SAVE.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_SLGORT) TYPE  LGORT_D DEFAULT 0032
*"     VALUE(IM_DLGORT) TYPE  LGORT_D OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      IT_DATA TYPE  ZHU_MOVE_T OPTIONAL
*"----------------------------------------------------------------------

  DATA : IT_FINAL TYPE TABLE OF ZHU_MOVE,
         WA_FINAL TYPE ZHU_MOVE.

  DATA : LS_DATA TYPE ZHU_MOVE.
  DATA : IT_VEKP TYPE STANDARD TABLE OF VEKP.
  DATA :
         LS_VEKP TYPE VEKP,
         LS_EXREF TYPE ZWM_EXREF.
  DATA : LV_LGPLA TYPE LGPLA,
         LV_LGNUM TYPE LGNUM,
         LV_LGTYP TYPE LGTYP,
         LV_EXIDV TYPE EXIDV,
         LV_HU  TYPE EXIDV,
         LV_LGORT TYPE LGORT_D,
         LV_SLGORT TYPE LGORT_D,
         LV_DLGORT TYPE LGORT_D.

BREAK-POINT ID Z_V2CHECK.

  FIELD-SYMBOLS : <LFS_DATA> TYPE ZHU_MOVE.
*  if it_data[] is initial .
*    ex_return-message = 'No Data For Process'.
*    ex_return-type = 'E'.
*    return.
*  endif.

  IF IM_USER IS INITIAL .
    EX_RETURN-MESSAGE = 'NO USER'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.

  IF IM_WERKS IS INITIAL .
    EX_RETURN-MESSAGE = 'FILL SITE'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.
  IF IM_WERKS EQ 'DH24' OR IM_WERKS EQ 'DK10'.

    IF IM_WERKS = 'DH24'.
      LV_LGNUM = 'V2R'.
    ELSE.
      LV_LGNUM = 'V2B'.
    ENDIF.

  ELSE.
    MESSAGE E196(ZWM) WITH IM_WERKS.
  ENDIF.

*    IF im_werks = 'DH24'.
*      lv_lgnum = 'V2R'.
*    ELSE.
*      lv_lgnum = 'V2B'.
*    ENDIF.
*        else.
*    message e196(zwm) with im_werks.
*  endif.
*  ENDIF.


  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_SLGORT
    IMPORTING
      OUTPUT = LV_SLGORT.


  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      INPUT  = IM_DLGORT
    IMPORTING
      OUTPUT = LV_DLGORT.

  LOOP AT IT_DATA ASSIGNING <LFS_DATA>.

****  check hu if it is 2000 means external otheriwse internal

    IF <LFS_DATA>-EX_HU IS NOT INITIAL.
      LV_HU = <LFS_DATA>-EX_HU .
      CALL FUNCTION 'CONVERSION_EXIT_ALPHA_OUTPUT'
        EXPORTING
          INPUT  = LV_HU
        IMPORTING
          OUTPUT = LV_HU.
      IF  LV_HU+0(1) = '2'.
        SELECT SINGLE * FROM ZWM_EXREF INTO LS_EXREF WHERE EXIDV = LV_HU .
        IF SY-SUBRC IS INITIAL AND LS_EXREF-SAP_HU IS NOT INITIAL.
          LV_EXIDV = LS_EXREF-SAP_HU.
        ENDIF.


        CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
          EXPORTING
            INPUT  = <LFS_DATA>-EX_HU
          IMPORTING
            OUTPUT = <LFS_DATA>-EX_HU.


        <LFS_DATA>-SAP_HU  = LV_EXIDV .


      ELSE.
        CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
          EXPORTING
            INPUT  = <LFS_DATA>-EX_HU
          IMPORTING
            OUTPUT = <LFS_DATA>-SAP_HU.

        CLEAR <LFS_DATA>-EX_HU .
      ENDIF.
    ENDIF.


    TRANSLATE <LFS_DATA>-LGPLA TO UPPER CASE .

  ENDLOOP.


  CHECK IT_DATA[] IS NOT INITIAL .
  SELECT * FROM VEKP INTO TABLE IT_VEKP
              FOR ALL ENTRIES IN IT_DATA
              WHERE EXIDV = IT_DATA-SAP_HU.
    IF SY-SUBRC = 0.
   SORT IT_VEKP BY EXIDV.
   ENDIF.

  LOOP AT  IT_DATA ASSIGNING <LFS_DATA>.

    READ TABLE IT_VEKP INTO LS_VEKP WITH KEY EXIDV = <LFS_DATA>-SAP_HU
                                              BINARY SEARCH.

    IF  SY-SUBRC IS INITIAL  .

      WA_FINAL-VENUM = LS_VEKP-VENUM.

    ENDIF.

    WA_FINAL-EX_HU = <LFS_DATA>-EX_HU.
    WA_FINAL-SAP_HU = <LFS_DATA>-SAP_HU.
*    wa_final-loekz  = ls_data-loekz.
    WA_FINAL-LGNUM = LV_LGNUM.
    WA_FINAL-LGTYP = 'G32'.
    WA_FINAL-LGPLA =  <LFS_DATA>-LGPLA.
    WA_FINAL-SLGORT = LV_SLGORT.
    WA_FINAL-DLGORT = LV_DLGORT.
*wa_final-mblnr = ls_data-mblnr.
*wa_final-mjahr = ls_data-mjahr.
    WA_FINAL-ERNAM = SY-UNAME .
    WA_FINAL-ERDAT = SY-DATUM.
    WA_FINAL-ERZEIT = SY-UZEIT.
*    wa_final-aenam = sy-uname.
*    wa_final-aedat = sy-datum.
    WA_FINAL-IM_USER = IM_USER.
    APPEND WA_FINAL TO IT_FINAL.
    CLEAR : LS_DATA, LS_VEKP, IM_USER.
  ENDLOOP.

  MODIFY ZHU_MOVE FROM TABLE IT_FINAL.
  COMMIT WORK AND WAIT .


  DATA : LT_MOVE TYPE HUM_DATA_MOVE_TO_T ,
           LT_INTERNAL TYPE HUM_VENUM_T.


  DATA : LS_MOVE TYPE HUM_DATA_MOVE_TO ,
*         wa_final type zhu_move,
         LS_INTERNAL TYPE HUM_VENUM,
         LV_MSG TYPE STRING,
         ES_MESSAGE TYPE HUITEM_MESSAGES,
         LS_MESSAGE TYPE HUITEM_MESSAGES,
         ES_EMKPF TYPE EMKPF,
         LS_EMKPF TYPE EMKPF.

  LS_MOVE-LGORT = LV_DLGORT .
  APPEND LS_MOVE TO LT_MOVE.


  LOOP AT IT_FINAL INTO WA_FINAL.

    LS_INTERNAL-VENUM = WA_FINAL-VENUM.
    APPEND LS_INTERNAL TO LT_INTERNAL .

    CALL FUNCTION 'DEQUEUE_ALL'
      EXPORTING
        _SYNCHRON = 'X'.


    CALL FUNCTION 'HU_CREATE_GOODS_MOVEMENT'
     EXPORTING
       IF_EVENT             =  '0006'
*     IF_SIMULATE          = ' '
*     IF_COMMIT            = ' '
       IF_TCODE             = 'HUMO'
*     IS_IMKPF             =
       IT_MOVE_TO           = LT_MOVE
       IT_INTERNAL_ID       = LT_INTERNAL
*     IT_EXTERNAL_ID       =
   IMPORTING
*     EF_POSTED            =
       ES_MESSAGE           = LS_MESSAGE
*     ET_MESSAGES          =
       ES_EMKPF             = LS_EMKPF
*   CHANGING
*     CT_IMSEG             =
              .

    IF LS_EMKPF-MBLNR IS NOT INITIAL .
***  posted successfully

      CALL FUNCTION 'BAPI_TRANSACTION_COMMIT'
       EXPORTING
         WAIT          = 'X'
* IMPORTING
*   RETURN        =
                .

      UPDATE ZHU_MOVE SET : MBLNR = LS_EMKPF-MBLNR
                            MJAHR = LS_EMKPF-MJAHR
                            FLAG_HUMOVE = 'X'
                          WHERE SAP_HU = WA_FINAL-SAP_HU
                            AND   EX_HU = WA_FINAL-EX_HU.

    ELSE.
***  error

      CALL FUNCTION 'FORMAT_MESSAGE'
        EXPORTING
          ID        = LS_MESSAGE-MSGID
          LANG      = SY-LANGU
          NO        = LS_MESSAGE-MSGNO
          V1        = LS_MESSAGE-MSGV1
          V2        = LS_MESSAGE-MSGV2
          V3        = LS_MESSAGE-MSGV3
          V4        = LS_MESSAGE-MSGV4
        IMPORTING
          MSG       = LV_MSG
        EXCEPTIONS
          NOT_FOUND = 1
          OTHERS    = 2.
      IF SY-SUBRC <> 0.
* MESSAGE ID SY-MSGID TYPE SY-MSGTY NUMBER SY-MSGNO
*         WITH SY-MSGV1 SY-MSGV2 SY-MSGV3 SY-MSGV4.
      ENDIF.

      IF LV_MSG IS INITIAL.
        LV_MSG = 'ERROR'.
      ENDIF.

      UPDATE ZHU_MOVE SET : MSG = LV_MSG
                            MBLNR = ''
                            MJAHR = ''
                            FLAG_HUMOVE = ''
                          WHERE SAP_HU = WA_FINAL-SAP_HU
                            AND   EX_HU = WA_FINAL-EX_HU.

    ENDIF.

    CALL FUNCTION 'DEQUEUE_ALL'
      EXPORTING
        _SYNCHRON = 'X'.

    REFRESH LT_INTERNAL .

    CLEAR LS_INTERNAL .
    CLEAR WA_FINAL.
    CLEAR LV_MSG .
    CLEAR ES_MESSAGE.
    CLEAR ES_EMKPF.
  ENDLOOP.

  EX_RETURN-MESSAGE = 'Data Processed'.
  EX_RETURN-TYPE = 'S'.






*    MESSAGE e110(zwm) WITH ls_vekp-exidv.
*
*  ELSE.
*
*    ls_table-exidv = ls_vekp-exidv.
*    ls_table-venum = ls_vekp-venum.
*    ls_table-ex_hu = ls_exref-exidv.
*    ls_table-lgpla = lv_lgpla.
*    ls_table-lgnum = lv_lgnum.
*    ls_table-lgtyp = lv_lgtyp.
*
*    APPEND ls_table TO gt_table .
*    CLEAR ls_table .
*
*  ENDIF.
*  CLEAR ls_vekp.
ENDFUNCTION.