FUNCTION ZWM_RFC_GRT_PUTWAY_POST.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_CRATE) TYPE  ZZCRATE OPTIONAL
*"     VALUE(IM_LGPLA) TYPE  LGPLA OPTIONAL
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"----------------------------------------------------------------------
  DATA: LV_LGORT TYPE LGORT_D,
        LV_VLTYP TYPE LTAP_VLTYP,
        LV_VLBER TYPE LTAP_VLBER,
        LV_NLBER TYPE LTAP_NLBER.

  LV_LGORT = '0001'.
  LV_VLTYP = 'V01'.
  LV_VLBER = '001'.
  LV_NLBER = '001'.
BREAK-POINT ID Z_V2CHECK.
  CALL FUNCTION 'ZWM_RFC_GRT_PUTWAY_CRATE_VAL'
    EXPORTING
      IM_WERKS  = IM_WERKS
      IM_CRATE  = IM_CRATE
      IM_LGPLA  = IM_LGPLA
    IMPORTING
      EX_RETURN = EX_RETURN.
IF EX_RETURN-TYPE NE 'E'.
  PERFORM F_CLEAR_V04_FROM_MSA_BIN USING  IM_CRATE
                                          IM_LGPLA
                                          IM_WERKS
                                          LV_LGORT
                                          LV_VLTYP
                                          LV_VLBER
                                          LV_NLBER
                                          EX_RETURN.
ENDIF.

ENDFUNCTION.