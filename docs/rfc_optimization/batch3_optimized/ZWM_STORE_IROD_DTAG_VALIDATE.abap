FUNCTION ZWM_STORE_IROD_DTAG_VALIDATE.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_IROD) TYPE  ZIROD1
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"----------------------------------------------------------------------
  DATA:
    LV_LGPLA TYPE LGPLA,
    LV_WERKS TYPE WERKS_D,
    LV_USER  TYPE WWWOBJID.

  LV_USER = TO_UPPER( |{ IM_USER ALPHA = IN }| ).
  LV_WERKS = IM_WERKS.

  IF IM_USER IS INITIAL AND IM_WERKS IS INITIAL.
    EX_RETURN-MESSAGE = 'User Id Cannot Be Blank.'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.

  IF IM_WERKS IS INITIAL.
    SELECT
    SINGLE WERKS
      FROM ZWM_USR02
     WHERE UPPER( BNAME ) EQ @IM_USER
      INTO @LV_WERKS.
    IF SY-SUBRC IS NOT INITIAL.
      EX_RETURN-MESSAGE = 'User Id Cannot Be Blank.'.
      EX_RETURN-TYPE = 'E'.
      RETURN.
    ENDIF.
  ENDIF.


  SELECT *
    FROM ZWM_ST01_IROD_MC
   WHERE WERKS EQ @LV_WERKS
     AND IROD  EQ @IM_IROD
    INTO @DATA(LS_IROD)
      UP TO 1 ROWS.
  ENDSELECT.

  IF SY-SUBRC IS NOT INITIAL.
    EX_RETURN-TYPE = 'E'.
    MESSAGE E001(00) WITH |Invalid IROD { IM_IROD } | INTO EX_RETURN-MESSAGE.
    RETURN.
  ENDIF.

*  SELECT *
*    FROM ZWM_ST01_BIN_TAG
*   WHERE WERKS EQ @LV_WERKS
*     AND LGNUM EQ @IM_LGNUM
*     AND IROD  EQ @IM_IROD
*    INTO @DATA(LS_TAG)
*      UP TO 1 ROWS.
*  ENDSELECT.
*
*  IF SY-SUBRC IS NOT INITIAL.
*    EX_RETURN-TYPE = 'E'.
*    MESSAGE E001(00) WITH |Irod { IM_IROD } not yet tagged| INTO EX_RETURN-MESSAGE.
*  ELSE.
*    EX_RETURN-TYPE = 'S'.
*    EX_RETURN-MESSAGE = 'Validated Successfully'.
*  ENDIF.
*
*  EX_DATA = VALUE #( WERKS = LV_WERKS
*                     LGNUM = IM_LGNUM
*                     LGTYP = LV_WERKS+3(*)
*                     IROD  = LS_TAG-IROD
*                     LGPLA = LS_TAG-LGPLA ).

ENDFUNCTION.