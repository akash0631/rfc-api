FUNCTION ZWM_STORE_PUSHTOSAP_0001_IROD.
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
  DATA : lt_push TYPE STANDARD TABLE OF zwm_stpushtodis1 .

  DATA :
        ls_push TYPE zwm_stpushtosap2,
        ls_data TYPE zwm_st_pushtosap_1stock,
        lv_count TYPE posnr,
        lv_len TYPE numc3,
        lv_doc TYPE zpicnr.


  IF et_data[] IS INITIAL.
    ex_return-message  = 'No data in File'.
    ex_return-type = 'E'.
    RETURN .

  ENDIF.

  CALL FUNCTION 'NUMBER_GET_NEXT'
EXPORTING
  nr_range_nr                   = '08'
  object                        = 'ZWM_HST'
*   QUANTITY                      = '1'
*   SUBOBJECT                     = ' '
*   TOYEAR                        = '0000'
*   IGNORE_BUFFER                 = ' '
IMPORTING
 number                        = lv_doc
*   QUANTITY                      =
*   RETURNCODE                    =
EXCEPTIONS
interval_not_found            = 1
number_range_not_intern       = 2
object_not_found              = 3
quantity_is_0                 = 4
quantity_is_not_1             = 5
interval_overflow             = 6
buffer_overflow               = 7
OTHERS                        = 8
        .
  IF sy-subrc <> 0.
    MESSAGE ID sy-msgid TYPE sy-msgty NUMBER sy-msgno
            WITH sy-msgv1 sy-msgv2 sy-msgv3 sy-msgv4.
  ENDIF.

  LOOP AT et_data INTO ls_data.

*
*  if ls_data-quantity gt 999.
*    ex_return-message  = 'Un expected Qty in file'.
*    ex_return-type = 'E'.
*    RETURN .
*  endif.

  lv_len  = strlen( ls_data-gandola ).

  if lv_len gt 10.
    ex_return-message  = 'Un expected Bin length'.
    ex_return-type = 'E'.
    RETURN .
  endif.

    IF  LS_DATA-SITE+0(1) EQ    'H' OR LS_DATA-SITE+0(1) EQ 'U' .
    ELSE.
    ex_return-message  = 'Invalid Store Code'.
    ex_return-type = 'E'.
    RETURN .
endif.

*    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
*      EXPORTING
*        input  = ls_data-article
*      IMPORTING
*        output = ls_push-matnr.
ls_push-matnr = ls_data-article.
    lv_count = lv_count + 1 .
    ls_push-picnr = lv_doc.
    ls_push-posnr = lv_count.
    TRANSLATE ls_data-site to UPPER CASE.
    TRANSLATE ls_data-gandola to UPPER CASE.
    TRANSLATE ls_data-emp_code to UPPER CASE.
    ls_push-werks = ls_data-site.
    ls_push-lgnum = 'SDC'.
    ls_push-zlgpla =  ls_data-gandola.
    ls_push-menge =  ls_data-quantity.
    ls_push-art_type =  ls_data-art_type.
    ls_push-zsize =  ls_data-zsize.
    ls_push-meins =  'EA'.
    ls_push-emp_code = im_user.
    ls_push-erdat = sy-datum.
    ls_push-erzet = sy-uzeit.
    ls_push-ernam = im_user.
    APPEND ls_push TO lt_push .

    CLEAR ls_push .
    CLEAR ls_data .
  ENDLOOP.

  MODIFY zwm_stpushtosap2 FROM TABLE lt_push.
  IF sy-subrc IS INITIAL .
    COMMIT WORK AND WAIT .
    CONCATENATE 'Data Pushed' lv_doc INTO ex_return-message SEPARATED BY '-'.
    ex_return-type = 'S'.
    RETURN .
  ELSE.
    ex_return-message  = 'Data Not Pushed'.
    ex_return-type = 'E'.
    RETURN .
  ENDIF.


ENDFUNCTION.