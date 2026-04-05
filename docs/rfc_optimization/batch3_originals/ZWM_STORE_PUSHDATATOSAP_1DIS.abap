FUNCTION ZWM_STORE_PUSHDATATOSAP_1DIS.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_NATURE) TYPE  CHAR1 DEFAULT 'D'
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_DATA TYPE  ZWM_ST_PUSHTOSAP_1STOCK_T
*"----------------------------------------------------------------------
  BREAK-POINT ID Z_V2CHECK.
  DATA : LT_PUSH TYPE STANDARD TABLE OF ZWM_STPUSHTODIS1 .

  DATA :
    LS_PUSH  TYPE ZWM_STPUSHTODIS1,
    LS_DATA  TYPE ZWM_ST_PUSHTOSAP_1STOCK,
    LV_COUNT TYPE POSNR,
    LV_LEN   TYPE NUMC3,
    LV_DOC   TYPE ZPICNR.


  IF ET_DATA[] IS INITIAL.
    EX_RETURN-MESSAGE  = 'No data in File'.
    EX_RETURN-TYPE = 'E'.
    RETURN .

  ENDIF.

  CALL FUNCTION 'NUMBER_GET_NEXT'
    EXPORTING
      NR_RANGE_NR             = '07'
      OBJECT                  = 'ZWM_HST'
*     QUANTITY                = '1'
*     SUBOBJECT               = ' '
*     TOYEAR                  = '0000'
*     IGNORE_BUFFER           = ' '
    IMPORTING
      NUMBER                  = LV_DOC
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

  LOOP AT ET_DATA INTO LS_DATA.


*    IF LS_DATA-QUANTITY GT 999.
*      EX_RETURN-MESSAGE  = 'Un expected Qty in file'.
*      EX_RETURN-TYPE = 'E'.
*      RETURN .
*    ENDIF.

    LV_LEN  = STRLEN( LS_DATA-GANDOLA ).

    IF LV_LEN GT 10.
      EX_RETURN-MESSAGE  = 'Un expected Bin length'.
      EX_RETURN-TYPE = 'E'.
      RETURN .
    ENDIF.


    IF  LS_DATA-SITE+0(1) EQ 'H' OR LS_DATA-SITE+0(1) EQ 'U' .
    ELSE.
      EX_RETURN-MESSAGE  = 'Invalid Store Code'.
      EX_RETURN-TYPE = 'E'.
      RETURN .
    ENDIF.

*    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
*      EXPORTING
*        input  = ls_data-article
*      IMPORTING
*        output = ls_push-matnr.
    LS_PUSH-MATNR = LS_DATA-ARTICLE.
    LV_COUNT = LV_COUNT + 1 .
    LS_PUSH-PICNR = LV_DOC.
    LS_PUSH-POSNR = LV_COUNT.
    LS_PUSH-WERKS = LS_DATA-SITE.
    LS_PUSH-LGNUM = 'SDC'.
    LS_PUSH-ZLGPLA =  LS_DATA-GANDOLA.
    LS_PUSH-MENGE =  LS_DATA-QUANTITY.
    LS_PUSH-MEINS =  'EA'.
    LS_PUSH-EMP_CODE = LS_DATA-EMP_CODE.
    LS_PUSH-ERDAT = SY-DATUM.
    LS_PUSH-ERZET = SY-UZEIT.
    LS_PUSH-ERNAM = IM_USER.
    APPEND LS_PUSH TO LT_PUSH .

    CLEAR LS_PUSH .
    CLEAR LS_DATA .
  ENDLOOP.

  MODIFY ZWM_STPUSHTODIS1 FROM TABLE LT_PUSH.
  IF SY-SUBRC IS INITIAL .
    COMMIT WORK AND WAIT .
    CONCATENATE 'Data Pushed' LV_DOC INTO EX_RETURN-MESSAGE SEPARATED BY '-'.
    EX_RETURN-TYPE = 'S'.
    RETURN .
  ELSE.
    EX_RETURN-MESSAGE  = 'Data Not Pushed'.
    EX_RETURN-TYPE = 'E'.
    RETURN .
  ENDIF.


ENDFUNCTION.