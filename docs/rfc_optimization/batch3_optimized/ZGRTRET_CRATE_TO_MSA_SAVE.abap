FUNCTION ZGRTRET_CRATE_TO_MSA_SAVE.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_CRATE) TYPE  ZZCRATE OPTIONAL
*"     VALUE(IM_BIN) TYPE  LGPLA OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_TANUM) TYPE  TANUM
*"----------------------------------------------------------------------
  DATA : LV_NLPLA TYPE LTAP_NLPLA.
  DATA : LS_LAGP TYPE LAGP.
  DATA : LV_LGTYP     TYPE LGTYP,
         LV_LGPLA     TYPE LGPLA,
         LV_LGTYP_NEW TYPE LGTYP.
  DATA : LV_TANUM TYPE TANUM,
         LV_MSG   TYPE BAPI_MSG.
  DATA : LS_ECOMPUT TYPE ZECOM_CANCELPUT,
         LT_ECOMPUT TYPE STANDARD TABLE OF ZECOM_CANCELPUT.

  IF IM_USER IS INITIAL.
    EX_RETURN-TYPE = 'E'.
    EX_RETURN-MESSAGE = 'HHT User Cannot Be Blank.'.
    RETURN.
  ENDIF.

  IF IM_CRATE IS INITIAL.
    EX_RETURN-TYPE = 'E'.
    EX_RETURN-MESSAGE = 'Crate Cannot Be Blank.'.
    RETURN.
  ENDIF.

  IF IM_BIN IS INITIAL.
    EX_RETURN-TYPE = 'E'.
    EX_RETURN-MESSAGE = 'Bin Cannot Be Blank.'.
    RETURN.
  ELSE.
    LV_LGPLA = IM_BIN.
  ENDIF.

  DATA(L_USERID) = IM_USER .
  TRANSLATE L_USERID TO UPPER CASE.

  SELECT SINGLE *
  FROM ZWM_USR02
  INTO @DATA(LS_USR02)
        WHERE BNAME EQ @L_USERID.

  IF SY-SUBRC IS NOT INITIAL.

    CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
      EXPORTING
        INPUT  = L_USERID
      IMPORTING
        OUTPUT = L_USERID.

    SELECT SINGLE *
    FROM ZWM_USR02
    INTO LS_USR02
    WHERE BNAME EQ L_USERID.
    DATA(LV_WERKS) = LS_USR02-WERKS.
  ELSE.
    LV_WERKS = LS_USR02-WERKS.
  ENDIF.

  IF lV_WERKS = 'DH24'.
    LV_LGTYP_NEW = 'E01'.
  ELSEIF LV_WERKS = 'DH26'.
    LV_LGTYP_NEW = 'E02'.
  ENDIF.


  TRANSLATE LV_LGPLA TO UPPER CASE.
  SELECT SINGLE * FROM ZWM_CRATE INTO @DATA(LS_CRATE) WHERE LGPLA = @LV_LGPLA.
  IF SY-SUBRC IS INITIAL.
    EX_RETURN-TYPE = 'E'.
    CONCATENATE 'BIN' LV_LGPLA 'already tagged with' LS_CRATE-CRATE INTO EX_RETURN-MESSAGE.

    RETURN.
  ENDIF.


  SELECT SINGLE lgnum lgtyp lgpla kzler
    FROM LAGP INTO LS_LAGP  WHERE LGNUM = 'V2R'
  AND LGTYP = LV_LGTYP_NEW "'E01'
  AND LGPLA = LV_LGPLA
  AND KZLER = 'X'
  AND LZONE = 'X'.

  IF SY-SUBRC IS NOT INITIAL.
    EX_RETURN-MESSAGE = 'Invalid Bin Or Is Not Empty'.
    EX_RETURN-TYPE = 'E'.
    RETURN.
  ENDIF.



*  LV_LGTYP = 'V08'.
*  LV_LGTYP = 'V15'.
  LV_LGTYP = 'V16'.
  PERFORM CREATE_REJECT_CRATE_TO_MSA_TO USING LV_WERKS IM_CRATE LV_LGTYP 'V2R' IM_BIN CHANGING LV_TANUM LV_MSG.
  IF LV_TANUM IS NOT INITIAL.
    EX_RETURN-TYPE = 'S'.
    EX_RETURN-MESSAGE = LV_MSG.
    EX_TANUM = LV_TANUM.
    UPDATE ZPTLR_RETPUT SET : TANUM2 = LV_TANUM
                              LGPLA2 = IM_BIN
                              ERDAT = SY-DATUM
                              ERNAM = IM_USER
                              WHERE CRATE = IM_CRATE AND LGPLA2 = ''.
*    WAIT UP TO 2 SECONDS.
    BREAK SAP_ABAP.


*    SELECT * FROM zwm_crate
*      INTO TABLE @DATA(lt_crate)
*      WHERE crate = @im_crate AND lgnum = 'V2R'.
*    IF sy-subrc IS  INITIAL.
*      LOOP AT lt_crate ASSIGNING FIELD-SYMBOL(<lwa_crate>).
*        <lwa_crate>-msa_empty = ''.
*        <lwa_crate>-flr_empty = ''.
*        <lwa_crate>-lgpla = im_bin.
*        <lwa_crate>-ebeln = ''.
*      ENDLOOP.
*    ENDIF.


    UPDATE ZWM_CRATE SET : MSA_EMPTY = ''
                          FLR_EMPTY = ''
                          LGPLA = IM_BIN
                          EBELN = ''
                          IM_USER = IM_USER
                          WHERE CRATE = IM_CRATE AND LGNUM = 'V2R'.
*    WAIT UP TO 2 SECONDS.
    UPDATE LAGP SET : LZONE = ''
                      UNAME = SY-UNAME LAEDT = SY-DATUM
                      KOBER = 'G01' " update picking area
                WHERE LGNUM = 'V2R'
                  AND LGTYP = LV_LGTYP_NEW  "'E01'
                  AND LGPLA = IM_BIN .

*    IF lt_crate[] IS NOT INITIAL.
*      MODIFY zwm_crate FROM TABLE lt_crate[].
*    ENDIF.

*    COMMIT WORK AND WAIT.
    RETURN.
  ELSE.
    EX_RETURN-TYPE  = 'E'.
    EX_RETURN-MESSAGE = LV_MSG.
    RETURN.
  ENDIF.



ENDFUNCTION.