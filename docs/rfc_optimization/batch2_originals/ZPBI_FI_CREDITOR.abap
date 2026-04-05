FUNCTION zpbi_fi_creditor.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_BUKRS) TYPE  BUKRS DEFAULT 1100
*"     VALUE(IM_DATE_FROM) TYPE  CPUDT OPTIONAL
*"     VALUE(IM_DATE_TO) TYPE  CPUDT OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"  TABLES
*"      ET_CREDITOR STRUCTURE  ZSTRU_CREDIOR OPTIONAL
*"      IT_LIFNR STRUCTURE  RANGELIFNR OPTIONAL
*"----------------------------------------------------------------------

  DATA:
    l_due_date TYPE datum,
    l_due_day  TYPE i.

  DATA:
    lr_bukrs TYPE RANGE OF bukrs,
    lr_date  TYPE RANGE OF cpudt.

  IF im_bukrs IS INITIAL AND im_date_from IS INITIAL AND im_date_to IS INITIAL.
    ex_return = VALUE #( type = c_msgty_e message = 'Please enter the Company code and Date').
    RETURN.
  ENDIF.

  IF im_bukrs IS NOT INITIAL.
    lr_bukrs = VALUE #( sign = c_sign_i ( option = c_option_eq  low = im_bukrs ) ).
  ENDIF.

  IF NOT ( im_date_from IS INITIAL AND im_date_to IS INITIAL ).

    lr_date = VALUE #( sign = c_sign_i
                       option = COND #( WHEN im_date_to IS NOT INITIAL THEN c_option_bt ELSE c_option_eq )
                      ( low = im_date_from high = im_date_to ) ).
  ENDIF.

  LOOP AT it_lifnr ASSIGNING FIELD-SYMBOL(<lfs_lifnr>).
    <lfs_lifnr>-sign = c_sign_i.
    <lfs_lifnr>-option = COND #( WHEN <lfs_lifnr>-high IS NOT INITIAL THEN c_option_bt ELSE c_option_eq ).
  ENDLOOP.

  SELECT *
    FROM tgsbt
    INTO TABLE @DATA(lt_tgsbt)
   WHERE spras = @sy-langu.
  IF sy-subrc IS INITIAL.
    SORT lt_tgsbt BY spras gsber.
  ENDIF.

  SELECT *
    FROM zbus_area
    INTO TABLE @DATA(lt_bus_area).

  IF sy-subrc IS INITIAL.
    SORT lt_bus_area BY gsber.
  ENDIF.

  SELECT *
    FROM tvzbt
    INTO TABLE @DATA(lt_tvzbt)
    WHERE spras EQ @sy-langu.
  IF sy-subrc IS INITIAL.
    SORT lt_tvzbt BY zterm.
  ENDIF.

  SELECT *
    FROM t052
    INTO TABLE @DATA(lt_t052).
  IF sy-subrc IS INITIAL.
    SORT lt_t052 BY zterm.
  ENDIF.

  SELECT bukrs,
         belnr,
         gjahr,
         gsber,
         lifnr,
         budat,
         shkzg,
         zfbdt,
         xblnr,
         bldat,
         cpudt,
         sgtxt,
         dmbtr,
         blart
    FROM bsik_view
    INTO TABLE @DATA(lt_bsik)
   WHERE lifnr IN @it_lifnr
     AND bukrs IN @lr_bukrs
     AND cpudt IN @lr_date.

  SORT lt_bsik ASCENDING BY bukrs belnr gjahr.
  DELETE ADJACENT DUPLICATES FROM lt_bsik COMPARING bukrs belnr gjahr.
  DATA lt_bsik_t LIKE lt_bsik.

  lt_bsik_t = CORRESPONDING #( lt_bsik ).
  SORT lt_bsik_t ASCENDING BY lifnr.
  DELETE ADJACENT DUPLICATES FROM lt_bsik_t COMPARING lifnr.

  SELECT a~lifnr,
         a~name1,
         a~ort01,
         a~bahns
    FROM lfa1 AS a
   INNER JOIN @lt_bsik_t AS b
      ON a~lifnr EQ b~lifnr
    INTO TABLE @DATA(lt_lfa1).

  SELECT a~lifnr,
         a~bukrs,
         a~zterm
    FROM lfb1 AS a
   INNER JOIN @lt_lfa1 AS b
      ON a~lifnr EQ b~lifnr
   WHERE a~bukrs IN @lr_bukrs
    INTO TABLE @DATA(lt_lfb1).

  IF sy-subrc IS INITIAL.
    SORT lt_lfb1 BY lifnr bukrs.
  ENDIF.

  SELECT a~rbukrs,
         a~gjahr,
         a~belnr,
         a~awref
    FROM acdoca AS a
   INNER JOIN @lt_bsik AS b
      ON a~rbukrs EQ b~bukrs
     AND a~belnr  EQ b~belnr
     AND a~gjahr  EQ b~gjahr
   WHERE a~rldnr  EQ @c_rldnr_0l
    INTO TABLE @DATA(lt_acd).

  IF sy-subrc IS INITIAL.
    SORT lt_acd ASCENDING BY rbukrs gjahr belnr.
    DELETE ADJACENT DUPLICATES FROM lt_acd COMPARING belnr awref.
  ENDIF.

  SELECT a~ebeln,
         a~ebelp,
         a~gjahr,
         a~belnr,
         a~lfgja,
         a~lfbnr
    FROM ekbe AS a
   INNER JOIN @lt_acd AS b
      ON a~belnr  EQ b~belnr
     AND a~gjahr  EQ b~gjahr
    INTO TABLE @DATA(lt_ekbe).

  IF sy-subrc IS INITIAL.
    SORT lt_ekbe ASCENDING BY gjahr belnr.
    DELETE ADJACENT DUPLICATES FROM lt_ekbe COMPARING ebeln lfbnr.
  ENDIF.

  SELECT a~ebeln,
         a~mblnr,
         a~edocno
    FROM  zwm_putway AS a
   INNER JOIN @lt_ekbe AS b
      ON a~ebeln EQ b~ebeln
     AND mblnr EQ b~lfbnr
    INTO TABLE @DATA(lt_putway).

  IF sy-subrc IS INITIAL.
    SORT lt_putway ASCENDING BY ebeln mblnr.
    DELETE ADJACENT DUPLICATES FROM lt_putway COMPARING ebeln mblnr.
  ENDIF.

  SELECT a~edocno,
         a~invno6 as invno,
         a~invdt,
         a~meins,
         a~inv_qty,
         a~inv_val
    FROM zgenhd AS a
   INNER JOIN @lt_putway AS b
      ON a~edocno EQ  b~edocno
    INTO TABLE @DATA(lt_zgen).

  IF sy-subrc IS INITIAL.
    SORT lt_zgen BY edocno.
  ENDIF.

  SELECT a~bukrs,
         a~belnr,
         a~gjahr,
         a~buzei,
         a~gsber,
         a~dmbtr,
         a~shkzg
    FROM bseg AS a
   INNER JOIN @lt_bsik AS b
      ON a~bukrs EQ b~bukrs
     AND a~belnr  EQ b~belnr
     AND a~gjahr  EQ b~gjahr
    INTO TABLE @DATA(lt_bseg).

  DELETE lt_bseg WHERE gsber IS INITIAL.

  DATA lt_bseg_temp LIKE lt_bseg.
  lt_bseg_temp = CORRESPONDING #( lt_bseg ).

  DELETE lt_bseg WHERE gsber EQ '1000'.

  LOOP AT lt_bsik ASSIGNING FIELD-SYMBOL(<lfs_bsik>).

    APPEND INITIAL LINE TO et_creditor ASSIGNING FIELD-SYMBOL(<lfs_data>).

    <lfs_data> = CORRESPONDING #( <lfs_bsik> ).
    <lfs_data>-gtext = VALUE #( lt_tgsbt[ gsber = <lfs_data>-gsber ]-gtext OPTIONAL ).

    IF <lfs_bsik>-gsber IS NOT INITIAL.
      <lfs_data>-gsber = <lfs_bsik>-gsber.
    ELSE.
      ASSIGN lt_bseg[ belnr = <lfs_bsik>-belnr
                      bukrs = <lfs_bsik>-bukrs
                      gjahr = <lfs_bsik>-gjahr ] TO FIELD-SYMBOL(<lfs_bseg>).

      IF <lfs_bseg> IS NOT ASSIGNED.
        ASSIGN lt_bseg_temp[ belnr = <lfs_bsik>-belnr
                             bukrs = <lfs_bsik>-bukrs
                             gjahr = <lfs_bsik>-gjahr ] TO <lfs_bseg>.
      ENDIF.

      IF <lfs_bseg> IS ASSIGNED.
        <lfs_data>-gsber = <lfs_bseg>-gsber.
      ENDIF.
    ENDIF.

    ASSIGN lt_bus_area[ gsber = <lfs_data>-gsber ] TO FIELD-SYMBOL(<lfs_bus_area>).
    IF <lfs_bus_area> IS ASSIGNED.
      <lfs_data> = CORRESPONDING #( BASE ( <lfs_data> ) <lfs_bus_area> ).
    ENDIF.

    ASSIGN lt_lfa1[ lifnr = <lfs_bsik>-lifnr ] TO FIELD-SYMBOL(<lfs_lfa1>).
    IF <lfs_lfa1> IS ASSIGNED.
      <lfs_data>-name1 = <lfs_lfa1>-name1.
      <lfs_data>-ort01 = <lfs_lfa1>-ort01.
      <lfs_data>-bahns = <lfs_lfa1>-bahns.
    ENDIF.

    IF <lfs_bsik>-shkzg = c_shkzg_h.
      <lfs_data>-cf_amt = <lfs_bsik>-dmbtr * -1.
      <lfs_data>-dmbtr_c = <lfs_bsik>-dmbtr.
    ELSEIF <lfs_bsik>-shkzg = c_shkzg_s.
      <lfs_data>-adv_amt = <lfs_bsik>-dmbtr.
      <lfs_data>-pi_amt  = <lfs_bsik>-dmbtr.
      <lfs_data>-cf_amt  = <lfs_bsik>-dmbtr .
    ENDIF.

    <lfs_data>-zterm = VALUE #( lt_lfb1[ bukrs = <lfs_bsik>-bukrs lifnr = <lfs_bsik>-lifnr ]-zterm OPTIONAL ).
    <lfs_data>-zterm_bez =  VALUE #( lt_tvzbt[ zterm  = <lfs_data>-zterm ]-vtext OPTIONAL ).
    <lfs_data>-ztag1  = VALUE #( lt_t052[ zterm = <lfs_data>-zterm ]-ztag1 OPTIONAL ).

    IF <lfs_bsik>-zfbdt GT sy-datum.
      <lfs_data>-due_date = <lfs_data>-pdc_date = <lfs_bsik>-zfbdt. "|{ <lfs_bsik>-zfbdt+6(2) }.{ <lfs_bsik>-zfbdt+4(2) }.{<lfs_bsik>-zfbdt(4)} |.
      <lfs_data>-pdc_grp  = sy-datum -  <lfs_bsik>-zfbdt.
    ELSE.
      l_due_date = <lfs_bsik>-budat + <lfs_data>-ztag1.

      <lfs_data>-due_date = l_due_date. "|{ due_date+6(2)}.{ due_date+4(2) }.{ due_date(4) }|.
      <lfs_data>-due_day = im_date_from - <lfs_bsik>-budat.
      <lfs_data>-od_day =  im_date_from - l_due_date.
      l_due_day = sy-datum - <lfs_bsik>-zfbdt.
*      due_day = sy-datum - due_date.
      <lfs_data>-age_group = COND #( WHEN l_due_day LE 30 THEN '0 - 30'
                                 WHEN l_due_day BETWEEN 31 AND 60 THEN '31 - 60'
                                 WHEN l_due_day BETWEEN 61 AND 90 THEN '61 - 90'
                                 ELSE ' > 90'
                                 ).
    ENDIF.

*    <lfs_data>-lifnr = |{ <lfs_data>-lifnr alpha = out }|.

    ASSIGN lt_acd[ rbukrs = <lfs_bsik>-bukrs
                   gjahr = <lfs_bsik>-gjahr
                   belnr = <lfs_bsik>-belnr ] TO FIELD-SYMBOL(<lfs_acd>).


    IF <lfs_acd> IS ASSIGNED.
      <lfs_data>-awref = <lfs_acd>-awref.

      ASSIGN lt_ekbe[ gjahr = <lfs_acd>-gjahr
                      belnr = <lfs_acd>-awref ] TO FIELD-SYMBOL(<lfs_ekbe>).

      IF <lfs_ekbe> IS ASSIGNED.
        <lfs_data>-lfgja = <lfs_ekbe>-lfgja.
        <lfs_data>-lfbnr = <lfs_ekbe>-lfbnr.
        ASSIGN lt_putway[ ebeln = <lfs_ekbe>-ebeln
                          mblnr = <lfs_ekbe>-lfbnr ] TO FIELD-SYMBOL(<lfs_putway>).

        IF <lfs_putway> IS ASSIGNED.
          <lfs_data>-edocno = <lfs_putway>-edocno.

          ASSIGN lt_zgen[ edocno = <lfs_putway>-edocno ] TO FIELD-SYMBOL(<lfs_zgen>).

          IF <lfs_zgen> IS ASSIGNED.
            <lfs_data>-invno    = <lfs_zgen>-invno.
            <lfs_data>-invdt    = <lfs_zgen>-invdt.
            <lfs_data>-inv_qty  = <lfs_zgen>-inv_qty.
            <lfs_data>-inv_val  = <lfs_zgen>-inv_val.
          ENDIF.
        ENDIF.
      ENDIF.
    ENDIF.

    UNASSIGN:
          <lfs_bseg>,
          <lfs_bus_area>,
          <lfs_lfa1>,
          <lfs_putway>,
          <lfs_ekbe>,
          <lfs_zgen>,
          <lfs_acd>.

  ENDLOOP.

  SORT et_creditor ASCENDING BY bukrs lifnr belnr.

ENDFUNCTION.