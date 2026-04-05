function zpbi_fms_prod_data.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_DATE_FROM) TYPE  DATUM OPTIONAL
*"     VALUE(IM_DATE_TO) TYPE  DATUM OPTIONAL
*"     VALUE(IM_INVOICED) TYPE  XFLD OPTIONAL
*"  TABLES
*"      IT_WERKS TYPE  RANGE_T_WERKS OPTIONAL
*"      IT_AUFNR TYPE  RANGE_T_AUFNR OPTIONAL
*"      IT_MPO TYPE  RANGE_T_AUFNR OPTIONAL
*"      ET_PROD_DATA STRUCTURE  ZSTRU_PROD_DATA OPTIONAL
*"----------------------------------------------------------------------

  data:
    ls_final   type zstru_prod_data,
    ls_final_n type zstru_prod_data,
    lr_date    type range of datum,
    lc_tmsc    type sytabix.

  data:
    lt_hdr       type kkbcq_t,
    lt_item      type standard table of kkbcs_out,
    lt_item_pm   type standard table of kkbcs_out,
    lt_dd07v     type standard table of dd07v,
    lt_aufnr     type range_t_aufnr,
    lt_prod_data type standard table of zstru_prod_data.

  if not ( im_date_from is initial and im_date_to is initial ).

    lr_date = value #( sign = c_sign_i
                       option = cond #( when im_date_to is not initial then c_option_bt else c_option_eq )
                      ( low = im_date_from high = im_date_to ) ).
  endif.

  perform f_modify_range changing it_werks[].
  perform f_modify_range changing it_aufnr[].
  perform f_modify_range changing it_mpo[].

  call function 'DDUT_DOMVALUES_GET'
    exporting
      name          = 'KKB_BEWEG'
      langu         = sy-langu
    tables
      dd07v_tab     = lt_dd07v
    exceptions
      illegal_input = 1.

  select *
    from t435t
    into table @data(lt_t435t)
    where spras eq @sy-langu.

  if sy-subrc is initial.
*   Do Nothing
  endif.

  select a~matnr,
         a~werks,
         b~mtart,
         b~zeinr,
         b~satnr,
         c~aufnr,
         c~aufpl,
         c~fsh_mprod_ord,
         c~gamng,
         c~plnty,
         c~plnnr,
         c~plnal,
         c~plnbez,
         d~name1,
         e~mtbez,
         v~vbeln
    from ( afko as c
    inner join aufk as au
       on c~aufnr eq au~aufnr )
    inner join  marc as a
       on a~matnr = c~plnbez
      and a~werks = au~werks
    inner join mara as b
       on b~matnr = c~plnbez
    inner join t001w as d
       on a~werks = d~werks
    inner join t134t as e
       on e~mtart = b~mtart
      and e~spras = @sy-langu
     left outer join vbrp as v
       on v~matnr eq a~matnr
      and v~werks eq a~werks
     into table @data(lt_data)
    where au~aufnr in @it_aufnr
      and au~werks in @it_werks
      and au~erdat in @lr_date
      and c~fsh_mprod_ord in @it_mpo.

  if sy-subrc = 0.

    case abap_true.
      when im_invoiced.
        delete lt_data where vbeln is initial.
      when others.
        delete lt_data where vbeln is not initial.
    endcase.

    sort lt_data by matnr werks mtart zeinr satnr aufnr fsh_mprod_ord.
    delete adjacent duplicates from lt_data comparing matnr werks mtart zeinr satnr aufnr fsh_mprod_ord.

  endif.

  if lt_data is initial.
    return.
  endif.

  select a~rueck,
         a~rmzhl,
         a~arbid,
         a~werks,
         a~ltxa1,
         a~aufnr,
         a~vornr,
         a~isdd,
         a~isdz,
         a~iedd,
         a~iedz
    from afru as a
    inner join @lt_data as b
       on a~aufnr eq b~aufnr
    into table @data(lt_ioopconf_t).

  if sy-subrc is initial.
*
  endif.

  data(lt_temp) = lt_data.
  sort lt_temp by aufnr werks.
  delete adjacent duplicates from lt_temp comparing aufnr werks.

  select a~*
    from zpp_po_opdata as a
   inner join @lt_temp as b
      on a~aufnr = b~aufnr
     and a~werks = b~werks
     and a~del_flag = ''
    into table @data(lt_opdata_t).

  if sy-subrc = 0.
*
  endif.

  select a~vornr,
         a~fsh_mprod_ord,
         a~aufnr,
         a~werks,
         a~matnr,
         zman_power,
         zsup_power,
         zperpcvalue,
         gsm,
         width,
         shrinkage,
         width_uom,
         lot_weight,
         gewei
    from zfms_mpo_detail1 as a
    inner join @lt_data as b
       on b~fsh_mprod_ord = a~fsh_mprod_ord
      and b~aufnr = a~aufnr
      and b~werks = a~werks
      and b~matnr = a~matnr
    into table @data(lt_mpo_detail).

  if sy-subrc is initial.
*
  endif.

  lt_temp = lt_data.
  sort lt_temp by aufpl.
  delete adjacent duplicates from lt_temp comparing aufpl.

  select
distinct a~aufnr,
         b~vornr,
         b~arbid as arbid,
         b~werks as werks,
         b~ltxa1,
         c~vgw01,
         c~vgw02,
         e~kokrs,
         e~kostl,
         e~lstar,
         d~arbpl,
         f~ktext
   from @lt_temp as a
  inner join afvc as b
     on a~aufpl eq b~aufpl
  inner join afvv as c
     on c~aufpl eq b~aufpl
    and c~aplzl eq b~aplzl
  inner join crhd as d
     on d~objid eq b~arbid
  inner join crco as e
     on e~objty = d~objty
    and e~objid = d~objid
   left outer join crtx as f
     on f~objty eq d~objty
    and f~objid eq d~objid
    and f~spras eq @sy-langu
   into table @data(lt_iooper_t).

  data(lt_crco_tmp) = lt_iooper_t.
  sort lt_crco_tmp by aufnr vornr.
  delete adjacent duplicates from lt_crco_tmp comparing aufnr vornr.

  select cast( sum( ( case b~shkzg when 'H' then  b~menge else -1 *  b~menge end ) ) as quan( 13, 3 ) ) as act_cons_qty,
         cast( sum( a~verpr * ( case b~shkzg when 'H' then  b~menge else -1 *  b~menge end ) ) as curr( 13, 2 ) ) as act_cons_val,
         c~mtart,
         o~aufnr,
         o~vornr
    from mbew as a
   inner join mseg as b
      on a~bwkey eq b~werks
     and a~matnr eq b~matnr
   inner join mara as c
      on c~matnr eq b~matnr
   inner join afru as d
      on b~mblnr eq d~wablnr
     and b~mjahr eq d~myear
     and b~aufnr eq d~aufnr
   inner join @lt_crco_tmp as o
      on o~aufnr eq d~aufnr
     and o~vornr eq d~vornr
   where b~bwart in ( '261', '262' )
   group by c~mtart,
            o~aufnr,
            o~vornr
    into table @data(lt_act).

  select
    cast( sum( b~bdmng ) as quan( 13, 3 ) ) as bud_cons_qty,
    cast( sum( a~verpr * b~bdmng ) as curr( 13, 2 ) ) as bud_cons_val,
         c~mtart,
         b~aufnr,
         b~vornr
    from mbew as a
   inner join resb as b
      on a~bwkey eq b~werks
     and a~matnr eq b~matnr
   inner join mara as c
      on c~matnr eq b~matnr
   inner join @lt_data as d
      on b~aufnr eq d~aufnr
     and b~bwart eq '261'
   group by c~mtart,
            b~aufnr,
            b~vornr
    into table @data(lt_bud).

  sort lt_act by mtart aufnr vornr.
  sort lt_bud by mtart aufnr vornr.
  sort lt_dd07v by domvalue_l.
  sort lt_t435t by vlsch.
  sort lt_mpo_detail by vornr fsh_mprod_ord aufnr werks matnr.
  data ls_iooper like line of lt_iooper_t.
  data lt_iooper like sorted table of ls_iooper with non-unique key aufnr kostl lstar with non-unique sorted key sort_key  components aufnr vornr.
  data ls_opdata like line of lt_opdata_t.
  data lt_opdata like sorted table of ls_opdata with non-unique key aufnr werks opt_no del_flag.
  data ls_ioopconf like line of lt_ioopconf_t.
  data lt_ioopconf like sorted table of ls_ioopconf with non-unique key aufnr arbid vornr.

  lt_iooper   = lt_iooper_t.
  lt_opdata   = lt_opdata_t.
  lt_ioopconf = lt_ioopconf_t.

  free:  lt_iooper_t,
         lt_opdata_t,
         lt_ioopconf_t.

  loop at lt_data assigning field-symbol(<lfs_data>) .

    clear:
       ls_final_n,
       lt_hdr,
       lt_item,
       lt_item_pm.

    call function 'ZFMS_PROD_GET_COST_DATA_NEW' destination 'NONE'
      exporting
        im_aufnr   = <lfs_data>-aufnr "lt_aufnr
      tables
        ex_hdr     = lt_hdr
        ex_item    = lt_item
        ex_item_pm = lt_item_pm.

    ls_final_n-plant      = <lfs_data>-werks.
    ls_final_n-name1      = <lfs_data>-name1.
    ls_final_n-zeinr      = <lfs_data>-zeinr.
    ls_final_n-article+0(18)    = |{ <lfs_data>-matnr alpha = in }|.
    ls_final_n-mpord_no   = <lfs_data>-fsh_mprod_ord.
    ls_final_n-aufnr      = <lfs_data>-aufnr.
    ls_final_n-mtbez      = <lfs_data>-mtbez.
    ls_final_n-pqty = value #( lt_hdr[ objnr = |OR{ <lfs_data>-aufnr }| wrttp = '01' ]-lst000 optional ).
    ls_final_n-aqty = value #( lt_hdr[ objnr = |OR{ <lfs_data>-aufnr }| wrttp = '04' ]-lst000 optional ).

    loop at lt_item assigning field-symbol(<lfs_item>).

      data(l_tabix) = sy-tabix.

      clear:
        ls_final.

      ls_final = corresponding #( ls_final_n ).
      assign lt_dd07v[ domvalue_l = <lfs_item>-beweg ] to field-symbol(<lfs_dd07v>).

      if <lfs_dd07v> is assigned.
        ls_final-tran = <lfs_dd07v>-ddtext.
      endif.

      ls_final-c_matnr     = <lfs_item>-matnr.
      ls_final-c_werks     = <lfs_item>-werks.
      ls_final-poqty       = <lfs_data>-gamng.
      ls_final-kstar       = <lfs_item>-kstar.
      ls_final-herku       = <lfs_item>-herku.
      ls_final-plankost_g  = <lfs_item>-plankost_g.
      ls_final-istkost_g   = <lfs_item>-istkost_g .

      assign lt_item_pm[ l_tabix ] to field-symbol(<lfs_item_pm>).
      if <lfs_item_pm> is assigned.
        ls_final-plankost_pm = <lfs_item_pm>-plankost_g.
        ls_final-istkost_pm  = <lfs_item_pm>-istkost_g.
      endif.

      assign lt_iooper[ aufnr = <lfs_data>-aufnr
                        kostl = <lfs_item>-kostl
                        lstar = <lfs_item>-lstar ] to field-symbol(<lfs_crco>).

      if <lfs_crco> is assigned.
        ls_final-vornr      = <lfs_crco>-vornr.
        ls_final-sproc_name = <lfs_crco>-ltxa1.
        ls_final-bgt_conqty  = reduce bdmng( init x type bdmng for ls_bud in lt_bud where ( aufnr = <lfs_data>-aufnr and vornr = <lfs_crco>-vornr ) next x += ls_bud-bud_cons_qty ).
        ls_final-act_conqty  = reduce bdmng( init x type bdmng for ls_act in lt_act where ( aufnr = <lfs_data>-aufnr and vornr = <lfs_crco>-vornr ) next x += ls_act-act_cons_qty ).
        ls_final-fab_bgt_ppcost  = value #( lt_bud[ mtart = '2111' aufnr = <lfs_data>-aufnr vornr = <lfs_crco>-vornr ]-bud_cons_val optional ).
        ls_final-acc_bgt_ppcost  = value #( lt_bud[ mtart = '2110' aufnr = <lfs_data>-aufnr vornr = <lfs_crco>-vornr ]-bud_cons_val optional ).
        ls_final-fab_act_ppcost  = value #( lt_act[ mtart = '2111' aufnr = <lfs_data>-aufnr vornr = <lfs_crco>-vornr ]-act_cons_val optional ).
        ls_final-acc_act_ppcost  = value #( lt_act[ mtart = '2110' aufnr = <lfs_data>-aufnr vornr = <lfs_crco>-vornr ]-act_cons_val optional ).

        ls_final-mproc_name = value #( lt_t435t[  vlsch = <lfs_crco>-vornr ]-txt optional ).

        ls_final-wcent_cd = <lfs_crco>-arbpl.
        ls_final-wcent_desc = <lfs_crco>-ktext.
        assign lt_opdata[ aufnr    = <lfs_data>-aufnr
                          werks    = <lfs_crco>-werks
                          opt_no   = <lfs_crco>-vornr
                          del_flag = '' ] to field-symbol(<lfs_opdata>).

        if <lfs_opdata> is assigned.

          ls_final-bgt_manp   = <lfs_opdata>-bgt_man.
          ls_final-bgt_stdt   = <lfs_opdata>-bgt_stdt.
          ls_final-bgt_sttm   = <lfs_opdata>-bgt_sttm.
          ls_final-bgt_enddt  = <lfs_opdata>-bgt_enddt.
          ls_final-bgt_endtm  = <lfs_opdata>-bgt_endtm.
          ls_final-bgt_tat    = <lfs_opdata>-bgt_tat.

          call function 'HR_99S_INTERVAL_BETWEEN_DATES'
            exporting
              begda = <lfs_opdata>-bgt_stdt
              endda = <lfs_opdata>-bgt_enddt
            importing
              days  = ls_final-bgtday.

        endif.

        assign lt_iooper[ aufnr = <lfs_data>-aufnr vornr = <lfs_crco>-vornr ] to field-symbol(<ls_iooper>) .

        if <ls_iooper> is assigned.
          ls_final-bgtmin  = <ls_iooper>-vgw01 .
          ls_final-bgtcost = <ls_iooper>-vgw02 .
        endif.


        assign lt_ioopconf[ aufnr = <lfs_data>-aufnr
                            arbid = <lfs_crco>-arbid
                            vornr = <lfs_crco>-vornr ] to field-symbol(<ls_ioopconf>).

        if <ls_ioopconf> is assigned.

          ls_final-act_stdt  = <ls_ioopconf>-isdd.
          ls_final-act_sttm  = <ls_ioopconf>-isdz.
          ls_final-act_enddt = <ls_ioopconf>-iedd.
          ls_final-act_endtm = <ls_ioopconf>-iedz.

          call function 'HR_99S_INTERVAL_BETWEEN_DATES'
            exporting
              begda = ls_final-act_stdt
              endda = ls_final-act_enddt
            importing
              days  = ls_final-actday.

          call function 'SWI_DURATION_DETERMINE'
            exporting
              start_date = ls_final-act_stdt
              end_date   = ls_final-act_enddt
              start_time = ls_final-act_sttm
              end_time   = ls_final-act_endtm
            importing
              duration   = lc_tmsc.

          ls_final-act_tat = lc_tmsc / 60.

        endif.

        read table lt_mpo_detail
           with key vornr = <lfs_crco>-vornr
                    fsh_mprod_ord = <lfs_data>-fsh_mprod_ord
                    aufnr = <lfs_data>-aufnr
                    werks = <lfs_data>-werks
                    matnr = <lfs_data>-matnr assigning field-symbol(<lfs_mpo_detail>)
                    binary search.

        if <lfs_mpo_detail> is assigned.

          ls_final-man_power  = <lfs_mpo_detail>-zman_power.
          ls_final-sup_power  = <lfs_mpo_detail>-zsup_power.
          ls_final-perpcvalue = <lfs_mpo_detail>-zperpcvalue.
          ls_final-gsm = <lfs_mpo_detail>-gsm.
          ls_final-width = <lfs_mpo_detail>-width.
          ls_final-shrinkage = <lfs_mpo_detail>-shrinkage.
          ls_final-width_uom = <lfs_mpo_detail>-width_uom.
          ls_final-lot_weight = <lfs_mpo_detail>-lot_weight.
          ls_final-gewei = <lfs_mpo_detail>-gewei.

        endif.

        unassign <lfs_mpo_detail>.

      endif.

      append ls_final to lt_prod_data. "et_prod_data.

    endloop.

    unassign:
          <lfs_mpo_detail>,
          <lfs_item>,
          <lfs_item_pm>,
          <lfs_dd07v>,
          <lfs_crco>,
          <ls_iooper>,
          <lfs_opdata>,
          <ls_ioopconf>.

  endloop.

  select a~matnr,
         a~matkl,
         b~maktx,
         c~wgbez
    from mara as a
   inner join makt as b
      on b~matnr eq a~matnr
     and b~spras eq @sy-langu
   inner join t023t as c
      on c~spras eq @sy-langu
     and c~matkl eq a~matkl
   inner join @lt_prod_data as d
      on d~c_matnr eq a~matnr
    into table @data(lt_mara).

  sort lt_mara by matnr.
  loop at lt_prod_data assigning field-symbol(<lfs_final>).
    read table lt_mara assigning field-symbol(<lfs_mara>)
    with key matnr = <lfs_final>-c_matnr
    binary search.

    if <lfs_mara> is assigned.
      <lfs_final>-c_maktx = <lfs_mara>-maktx.
      <lfs_final>-c_matkl = <lfs_mara>-matkl.
      <lfs_final>-c_wgbez = <lfs_mara>-wgbez.
    endif.

    unassign <lfs_mara>.
  endloop.

  et_prod_data[] = lt_prod_data.

endfunction.