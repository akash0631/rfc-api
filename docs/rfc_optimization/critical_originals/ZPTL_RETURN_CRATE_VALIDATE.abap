FUNCTION ZPTL_RETURN_CRATE_VALIDATE.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_CRATE) TYPE  ZZCRATE OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"----------------------------------------------------------------------

  DATA : LV_CRATE TYPE ZZCRATE.
  DATA : LS_CRATE TYPE ZWM_CRATE.
  DATA : LV_LGNUM TYPE LGNUM.
  CLEAR: EX_RETURN.


    EX_RETURN-TYPE = 'E'.
    EX_RETURN-MESSAGE = 'Do not use this process'.
    RETURN.

*  IF im_user IS INITIAL.
*    ex_return-type = 'E'.
*    ex_return-message = 'HHT User Cannot Be Blank.'.
*    RETURN.
*  ENDIF.
*
*  IF im_crate IS INITIAL.
*    ex_return-type = 'E'.
*    ex_return-message = 'Crate Cannot Be Blank.'.
*    RETURN.
*  ELSE.
*    lv_crate = im_crate.
*  ENDIF.
*  lv_lgnum = 'V2R'.
*  TRANSLATE lv_crate TO UPPER CASE.
*
*  SELECT SINGLE * FROM zwm_crate
*  INTO ls_crate
*  WHERE lgnum = lv_lgnum
*  AND crate = lv_crate
*  AND lgpla = ''
*  AND msa_empty = 'X'.
*  IF sy-subrc IS NOT INITIAL.
*    ex_return-message = 'Invalid Crate Or It Is Already Tagged To A Bin'.
*    ex_return-type = 'E'.
*    RETURN.
*  ELSE.
*
**    if ls_crate-msa_empty ne 'X'.
**      ex_return-MESSAGE = 'Crate is not MSA Empty'.
**      ex_return-TYPE = 'E'.
**      RETURN.
**     endif.
*
*    ex_return-message = 'Success'.
*    ex_return-type = 'S'.
*  ENDIF.
ENDFUNCTION.