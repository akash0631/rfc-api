FUNCTION ZWM_CRATE_IDENTIFIER_RFC.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_PLANT) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_CRATE) TYPE  ZZCRATE OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_BIN) TYPE  LGPLA
*"     VALUE(EX_AVAIL_QTY) TYPE  LQUA_VERME
*"     VALUE(EX_ST_BIN_TYPE) TYPE  LVS_LPTYP
*"----------------------------------------------------------------------

  IF IM_USER IS INITIAL.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'USER ID SHOULD NOT BE BLANK' ).
    RETURN.
  ENDIF.

  IF IM_PLANT IS INITIAL.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'PLANT SHOULD NOT BE BLANK' ).
    RETURN.
  ENDIF.

  IF IM_CRATE IS INITIAL.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'CRATE SHOULD NOT BE BLANK' ).
    RETURN.
  ENDIF.

  DATA : LV_QTY TYPE STRING.

**  VALIDATION ON USER AND PLANT
  IM_USER = |{ IM_USER ALPHA = IN }|.
  SELECT SINGLE WERKS FROM ZWM_USR02 INTO @DATA(LV_PLANT)
    WHERE BNAME = @IM_USER
     AND  WERKS = @IM_PLANT.

  IF SY-SUBRC NE 0.
    EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'PLANT AND USER DOESN''T MATCH').
    RETURN.
  ENDIF.

***fetched storage type
  SELECT SINGLE LGTYP , LGNUM FROM ZWM_DC_MASTER INTO ( @DATA(LV_LGTYP) , @DATA(LV_LGNUM) )
    WHERE WERKS = @IM_PLANT.

***Get Crate detials with storage type filtering
  IF SY-SUBRC EQ 0.
    SELECT SINGLE
      _LAGP~LGPLA, _LAGP~LGTYP,
      _LAGP~KZLER, _LAGP~LZONE,
      _LAGP~LPTYP,_CRATE~CRATE,
      SUM( _LQUA~VERME ) AS AVAIL_QTY
             FROM LAGP AS _LAGP  LEFT JOIN LQUA AS _LQUA
                                 ON _LQUA~LGPLA = _LAGP~LGPLA
                                 AND _LQUA~LGTYP = _LAGP~LGTYP
                                 AND _LQUA~LGNUM = _LAGP~LGNUM
                                 AND  _LQUA~WERKS  = @IM_PLANT
             LEFT JOIN ZWM_CRATE AS _CRATE
             ON _CRATE~LGPLA  = _LAGP~LGPLA
             AND _CRATE~LGTYP  = _LAGP~LGTYP
             AND _CRATE~LGNUM  = _LAGP~LGNUM
                     WHERE _CRATE~CRATE  = @IM_CRATE
                     AND  _LAGP~LGTYP  = @LV_LGTYP
                     AND  _LAGP~LGNUM  = @LV_LGNUM
                     GROUP BY
                          _LAGP~LGPLA,
                          _LAGP~LGTYP,
                          _LAGP~KZLER,
                          _LAGP~LZONE,
                          _LAGP~LPTYP,
                          _CRATE~CRATE
                     INTO @DATA(LS_BIN).
    IF SY-SUBRC = 0.
      EX_BIN        = LS_BIN-LGPLA.
      EX_AVAIL_QTY    = LS_BIN-AVAIL_QTY.
      EX_ST_BIN_TYPE  = LS_BIN-LPTYP.

      IF LS_BIN-AVAIL_QTY GT 0.
        EX_RETURN = VALUE #( TYPE = 'S' MESSAGE = 'BIN AND CRATE MAPPED WITH AVAILABLE STOCK ').
        RETURN.
      ELSE.
        EX_RETURN = VALUE #( TYPE = 'S' MESSAGE = 'BIN AND CRATE MAPPED WITH NO STOCK ').
        RETURN.
        ENDIF.
      ELSE.
        EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'CRATE DOESN''T MATCH ').
      ENDIF.

    ELSE.
      EX_RETURN = VALUE #( TYPE = 'S' MESSAGE = 'PLANT DOESN''T MATCH WITH STORAGE TYPE ').
      RETURN.
    ENDIF.


  ENDFUNCTION.