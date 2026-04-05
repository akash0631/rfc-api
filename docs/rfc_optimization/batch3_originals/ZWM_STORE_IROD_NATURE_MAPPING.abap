FUNCTION ZWM_STORE_IROD_NATURE_MAPPING .
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_IROD) TYPE  ZIROD1 OPTIONAL
*"     VALUE(IM_NATR) TYPE  ZTYPE1 OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      EX_DATA TYPE  ZWM_ST01_IROD_MC_TT
*"----------------------------------------------------------------------
  DATA:
    LV_USER  TYPE WWWOBJID,
    lv_werks TYPE werks_d,
    ls_iro type ZWM_ST01_IROD_MC.


  LV_USER = TO_UPPER( |{ IM_USER ALPHA = IN }| ).
  LV_WERKS = IM_WERKS.

  IF IM_USER IS INITIAL.
    EX_RETURN-MESSAGE = 'User Id Cannot Be Blank.'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
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

  SELECT *
    FROM ZWM_ST01_BIN_TAG
   WHERE WERKS EQ @LV_WERKS
*     AND LGNUM EQ @IM_LGNUM
     AND IROD  EQ @IM_IROD
    INTO @DATA(LS_TAG)
      UP TO 1 ROWS.
  ENDSELECT.

  IF SY-SUBRC IS NOT INITIAL.
    EX_RETURN-TYPE = 'E'.
    MESSAGE E001(00) WITH |Irod is not tag { IM_IROD }| INTO EX_RETURN-MESSAGE.
  ELSE.

   ls_iro-WERKS = IM_WERKS.
   ls_iro-IROD  = IM_IROD.
   ls_iro-TYPE  = IM_NATR.

update  ZWM_ST01_IROD_MC set type = im_natr where werks = lv_werks and irod = IM_IROD .
*   modify ZWM_ST01_IROD_MC from ls_iro.
   commit WORK.
  ENDIF.


ENDFUNCTION.