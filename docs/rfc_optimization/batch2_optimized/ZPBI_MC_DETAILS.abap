FUNCTION ZPBI_MC_DETAILS .
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_REFRESH) TYPE  XFLD OPTIONAL
*"  TABLES
*"      ET_MC_DATA STRUCTURE  ZTMC_MASTER OPTIONAL
*"----------------------------------------------------------------------

  DATA:
    LT_WGH01 TYPE STANDARD TABLE OF WGH01 WITH DEFAULT KEY,
    L_FLD    TYPE FIELDNAME,
    L_CNTR   TYPE NUMC2.

*  IF im_refresh EQ abap_true.
*    DELETE FROM ztmc_master.
*  ENDIF.
*
*  SELECT *
*    FROM
*    ztmc_master
*    INTO TABLE et_mc_data.

*  IF sy-subrc IS NOT INITIAL.

  SELECT *
    FROM T023T
    INTO TABLE @DATA(LT_T023T)
   WHERE SPRAS EQ @SY-LANGU.

  IF SY-SUBRC IS INITIAL.
    SORT LT_T023T BY MATKL.
  ENDIF.
  SELECT
   A~MATKL,
   A~MTART,
   A~SEG_CD,
   A~SEG,
   A~DIV_CD,
   A~DIVISION,
   A~SUB_DIV_CD,
   A~SUB_DIVISION,
   A~MAJ_CAT_CD,
   A~MAJ_CAT,
   A~MAJ_CAT_STAT,
   A~SUB_CAT_CD,
   A~SUB_CAT_DESC
    FROM ZMC_MASTER AS A
    INNER JOIN @LT_T023T AS B
    ON A~MATKL = B~MATKL
    INTO TABLE @DATA(LT_MC_MASTER).

  LOOP AT LT_T023T ASSIGNING FIELD-SYMBOL(<LFS_T023T>).

    APPEND INITIAL LINE TO ET_MC_DATA ASSIGNING FIELD-SYMBOL(<LFS_MC_DATA>).
    MOVE-CORRESPONDING <LFS_T023T> TO <LFS_MC_DATA>.

    CALL FUNCTION 'MERCHANDISE_GROUP_HIER_ART_SEL'
      EXPORTING
        MATKL       = <LFS_T023T>-MATKL
      TABLES
        O_WGH01     = LT_WGH01
      EXCEPTIONS
        NO_BASIS_MG = 1
        NO_MG_HIER  = 2
        OTHERS      = 3.

    IF SY-SUBRC <> 0.
* Implement suitable error handling here
    ENDIF.

    SORT LT_WGH01 BY STUFE DESCENDING.
    CLEAR L_CNTR.

    LOOP AT LT_WGH01 ASSIGNING FIELD-SYMBOL(<LFS_WGH01>).

      L_CNTR = L_CNTR + 1.

      L_FLD = |WWGHA_{ L_CNTR }|.
      ASSIGN COMPONENT L_FLD OF STRUCTURE <LFS_MC_DATA> TO FIELD-SYMBOL(<LFS_WWGHA>).
      L_FLD = |WWGHB_{ L_CNTR }|.
      ASSIGN COMPONENT L_FLD OF STRUCTURE <LFS_MC_DATA> TO FIELD-SYMBOL(<LFS_WWGHB>).

      IF <LFS_WWGHA> IS ASSIGNED AND <LFS_WWGHB> IS ASSIGNED.
        <LFS_WWGHA> = <LFS_WGH01>-WWGHA.
        <LFS_WWGHB> = <LFS_WGH01>-WWGHB.
      ENDIF.
      UNASSIGN:
           <LFS_WWGHA>,
           <LFS_WWGHB>.
    ENDLOOP.

    CLEAR:
       LT_WGH01.

  ENDLOOP.

*    INSERT ztmc_master FROM TABLE et_mc_data.
*    COMMIT WORK.

*  ENDIF.
LOOP AT ET_MC_DATA ASSIGNING FIELD-SYMBOL(<lfs_mc_data_t>).
  data(ls_mc_detail) = value #( LT_MC_MASTER[ matkl = <lfs_mc_data_t>-matkl ] optional ).

  <lfs_mc_data_t>-WWGHA_02 =  ls_mc_detail-SEG_CD.
  <lfs_mc_data_t>-WWGHB_02 =  ls_mc_detail-SEG.

  <lfs_mc_data_t>-WWGHA_03 = ls_mc_detail-div_cd.
  <lfs_mc_data_t>-WWGHB_03 = ls_mc_detail-DIVISION.

  <lfs_mc_data_t>-WWGHA_04 = ls_mc_detail-SUB_DIV_CD.
  <lfs_mc_data_t>-WWGHB_04 = ls_mc_detail-SUB_DIVISION.

  <lfs_mc_data_t>-WWGHA_05 = ls_mc_detail-MAJ_CAT_CD.
  <lfs_mc_data_t>-WWGHB_05 = ls_mc_detail-MAJ_CAT.

  <lfs_mc_data_t>-WWGHA_06 = ls_mc_detail-SUB_CAT_CD.
  <lfs_mc_data_t>-WWGHB_06 = ls_mc_detail-SUB_CAT_DESC.

ENDLOOP.

ENDFUNCTION.