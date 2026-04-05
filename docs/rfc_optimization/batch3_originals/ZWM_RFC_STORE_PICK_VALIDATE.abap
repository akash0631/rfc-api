FUNCTION zwm_rfc_store_pick_validate.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_WERKS) TYPE  WERKS_D
*"     VALUE(IM_PICNR) TYPE  ZPICNR
*"     VALUE(IM_LGNUM) TYPE  LGNUM OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"----------------------------------------------------------------------

  DATA: lt_st_pick TYPE STANDARD TABLE OF zst_pick.
  DATA: l_picnr  TYPE zpicnr.

  FIELD-SYMBOLS: <lfs_pick> TYPE zst_pick.

  IF im_picnr IS INITIAL .
    ex_return-message  = 'Blank Picklist not allowed'.
    ex_return-type = 'E'.
    EXIT.
  ENDIF.

  IF im_werks IS INITIAL .
    ex_return-message  = 'Blank site not allowed'.
    ex_return-type = 'E'.
    EXIT.
  ENDIF.

  CALL FUNCTION 'CONVERSION_EXIT_ALPHA_INPUT'
    EXPORTING
      input  = im_picnr
    IMPORTING
      output = l_picnr.

  IF im_lgnum IS INITIAL.
    SELECT * FROM zstore_bincon
      INTO TABLE @DATA(lt_bincon)
      WHERE werks = @im_werks.

    IF lt_bincon IS NOT INITIAL.
      READ TABLE lt_bincon ASSIGNING FIELD-SYMBOL(<lfs_bincon>)
                          WITH KEY werks = im_werks.
      IF <lfs_bincon>-active IS INITIAL.
        ex_return-type = c_error.
        ex_return-message = 'Picklist is not allowed. Please contact Inventory team.'.
        RETURN.
      ENDIF.
    ELSE.
      ex_return-type = c_error.
      ex_return-message = 'Picklist is not allowed. Please contact Inventory team.'.
      RETURN .
    ENDIF.
  ENDIF.

  SELECT * FROM zst_pick
          INTO  TABLE lt_st_pick
           WHERE picnr EQ l_picnr
*       AND lgnum EQ im_lgnum
       AND lgnum EQ 'SDC'
       AND werks EQ im_werks
*       AND lgort EQ im_lgort
       AND lgtyp EQ im_werks+1(3).
  IF sy-subrc IS  NOT INITIAL.
    ex_return-type = c_error.
    ex_return-message = 'Invalid Picklist'.
    RETURN .
  ELSE.
    LOOP AT lt_st_pick ASSIGNING <lfs_pick>.
      IF <lfs_pick>-pick_qty LE <lfs_pick>-picked_qty .
        <lfs_pick>-werks = ''.
      ENDIF.
    ENDLOOP.
    DELETE lt_st_pick WHERE werks IS INITIAL.

    IF lt_st_pick[] IS INITIAL.
      ex_return-type = c_error.
      ex_return-message = 'Picklist Closed'.
      RETURN .
    ENDIF.
  ENDIF.


ENDFUNCTION.