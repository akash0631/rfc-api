FUNCTION zwm_hu_quan.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(P_EXIDV) TYPE  EXIDV OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_QUAN) TYPE  VEPO-VEMNG
*"----------------------------------------------------------------------
* ══ Optimized by ABAP AI Studio — 04-Apr-2026 ══
* BEFORE: 5x SELECT *, loop to sum quantities, MESSAGE TYPE E (dump risk)
* AFTER:  SELECT SINGLE + SELECT SUM, no loop needed, safe error handling
* Expected: 5-10x faster

  DATA: lv_venum TYPE vekp-venum.

  BREAK-POINT ID z_v2check.

  IF p_exidv IS INITIAL.
    ex_return-type = 'E'.
    ex_return-message = 'HU barcode is empty'.
    RETURN.
  ENDIF.

  DATA(lv_exidv) = |{ p_exidv ALPHA = IN }|.

  " Check if external barcode (starts with 2)
  DATA(lv_stripped) = |{ lv_exidv ALPHA = OUT }|.

  IF lv_stripped+0(1) = '2'.
    " External HU — look up SAP HU from ZWM_EXREF (only need sap_hu field)
    SELECT SINGLE sap_hu FROM zwm_exref
      INTO @DATA(lv_sap_hu)
      WHERE exidv = @lv_exidv.
    IF sy-subrc <> 0.
      ex_return-type = 'E'.
      ex_return-message = 'External HU not found'.
      RETURN.
    ENDIF.
    SELECT SINGLE venum FROM vekp INTO @lv_venum
      WHERE exidv = @lv_sap_hu.
  ELSE.
    " Internal HU — direct VEKP lookup (only need venum)
    SELECT SINGLE venum FROM vekp INTO @lv_venum
      WHERE exidv = @lv_exidv.
  ENDIF.

  IF sy-subrc <> 0.
    ex_return-type = 'E'.
    ex_return-message = 'No HU found'.
    RETURN.
  ENDIF.

  " Sum quantity directly — replaces SELECT * + LOOP + accumulate
  SELECT SUM( vemng ) FROM vepo INTO @ex_quan
    WHERE venum = @lv_venum.

  IF ex_quan IS NOT INITIAL.
    ex_return-type = 'S'.
    ex_return-message = 'Quantity retrieved'.
  ELSE.
    ex_return-type = 'E'.
    ex_return-message = 'No Stock Found'.
  ENDIF.

ENDFUNCTION.
