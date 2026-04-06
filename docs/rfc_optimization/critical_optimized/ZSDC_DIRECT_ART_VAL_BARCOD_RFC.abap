FUNCTION ZSDC_DIRECT_ART_VAL_BARCOD_RFC.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IM_USER) TYPE  WWWOBJID OPTIONAL
*"     VALUE(IM_STORE_CODE) TYPE  WERKS_D OPTIONAL
*"     VALUE(IM_FLOOR) TYPE  LGPLA OPTIONAL
*"     VALUE(IM_BARCODE) TYPE  EAN11 OPTIONAL
*"  EXPORTING
*"     VALUE(EX_RETURN) TYPE  BAPIRET2
*"     VALUE(EX_BARCODE) TYPE  ZSDC_FLRBARCODE_ST
*"  TABLES
*"      ET_DATA TYPE  ZSDC_FLRBARCODE_TT OPTIONAL
*"----------------------------------------------------------------------

  " Local data declarations - only what's actually used
  DATA: LT_MESS  TYPE TABLE OF STRING,
        LT_DATA2 TYPE ZSDC_FLRBARCODE_TT.

  TRY.
      " Input validation with proper returns
      IF IM_USER IS INITIAL.
        EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'USER ID SHOULD NOT BE BLANK' ).
        RETURN.
      ENDIF.

      IF IM_STORE_CODE IS INITIAL.
        EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'STORE CODE SHOULD NOT BE BLANK' ).
        RETURN.
      ENDIF.

      IF IM_FLOOR IS INITIAL.
        EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'FLOOR SHOULD NOT BE BLANK' ).
        RETURN.
      ENDIF.

      IF IM_BARCODE IS INITIAL.
        EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'BARCODE SHOULD NOT BE BLANK' ).
        RETURN.
      ENDIF.

      " First attempt: Check ZDISC_ARTL for 'C' type articles
      SELECT DISTINCT
        A~EAN11 AS BARCODE,
        A~UMREZ,
        A~UMREZ AS SCAN_QTY,
        MARA~MATNR,
        B~WERKS,
        B~LGPLA,
        B~VERME AS QTY,
        C~LGPLA AS FLOOR_BIN,
        'C  ' AS ART_TYPE
      FROM MARM AS A
      INNER JOIN MARA ON MARA~MATNR = A~MATNR  " Direct join — MATNR format matches (HANA index used)
      INNER JOIN LQUA AS B ON B~MATNR = MARA~MATNR
        AND B~LGNUM = 'SDC'
        AND B~LGTYP = 'V09'
        AND B~LGORT = '0002'
      INNER JOIN ZSDC_FLRMSTR AS C ON C~WERKS = B~WERKS
        AND C~LGPLA = B~LGPLA
        AND C~MAJ_CAT_CD = MARA~MATKL
      INNER JOIN ZDISC_ARTL AS E ON E~WERKS = B~WERKS
        AND E~MATNR = B~MATNR
        AND E~EAN11 = A~EAN11
      WHERE A~EAN11 = @IM_BARCODE
        AND B~WERKS = @IM_STORE_CODE
      INTO TABLE @DATA(LT_DATA).

      " Second attempt: Check ZSDC_ART_STATUS if first query returns no data
      IF LT_DATA IS INITIAL.
        SELECT DISTINCT
          A~EAN11 AS BARCODE,
          A~UMREZ,
          A~UMREZ AS SCAN_QTY,
          B~MATNR,
          B~WERKS,
          B~LGPLA,
          B~VERME AS QTY,
          C~LGPLA AS FLOOR_BIN,
          E~ART_TYPE
        FROM MARM AS A
        INNER JOIN MARA ON MARA~MATNR = A~MATNR
        INNER JOIN LQUA AS B ON B~MATNR = MARA~MATNR
          AND B~LGNUM = 'SDC'
          AND B~LGTYP = 'V09'
          AND B~LGORT = '0002'
        INNER JOIN ZSDC_FLRMSTR AS C ON C~WERKS = B~WERKS
          AND C~LGPLA = B~LGPLA
          AND C~MAJ_CAT_CD = MARA~MATKL
        INNER JOIN ZSDC_ART_STATUS AS E ON E~STORE_CODE = B~WERKS
          AND E~ARTICLE_NO = B~MATNR
        WHERE A~EAN11 = @IM_BARCODE
          AND B~WERKS = @IM_STORE_CODE
        INTO TABLE @LT_DATA.
      ENDIF.

      " Third attempt: Default 'MIX' type if no specific article type found
      IF LT_DATA IS INITIAL.
        SELECT DISTINCT
          A~EAN11 AS BARCODE,
          A~UMREZ,
          A~UMREZ AS SCAN_QTY,
          MARA~MATNR,
          B~WERKS,
          B~LGPLA,
          B~VERME AS QTY,
          C~LGPLA AS FLOOR_BIN,
          'MIX' AS ART_TYPE
        FROM MARM AS A
        INNER JOIN MARA ON MARA~MATNR = A~MATNR
        INNER JOIN LQUA AS B ON B~MATNR = MARA~MATNR
          AND B~LGNUM = 'SDC'
          AND B~LGTYP = 'V09'
          AND B~LGORT = '0002'
        INNER JOIN ZSDC_FLRMSTR AS C ON C~WERKS = B~WERKS
          AND C~LGPLA = B~LGPLA
          AND C~MAJ_CAT_CD = MARA~MATKL
        WHERE A~EAN11 = @IM_BARCODE
          AND B~WERKS = @IM_STORE_CODE
        INTO TABLE @LT_DATA.
      ENDIF.

      " Remove duplicates based on material number
      SORT LT_DATA BY MATNR.
      DELETE ADJACENT DUPLICATES FROM LT_DATA COMPARING MATNR.

      " Check if any data was found
      IF LT_DATA IS INITIAL.
        EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = 'INVALID BARCODE WITH RESPECTIVE STORE CODE' ).
        RETURN.
      ENDIF.

      " Validate store consistency (though this should never fail due to WHERE clause)
      READ TABLE LT_DATA INTO DATA(WA_DATA) INDEX 1.
      IF WA_DATA-WERKS <> IM_STORE_CODE.
        APPEND |PLANT { WA_DATA-WERKS } DOES NOT MATCH WITH THE STORE { IM_STORE_CODE }| TO LT_MESS.
      ENDIF.

      " Process any validation messages
      IF LT_MESS IS NOT INITIAL.
        LOOP AT LT_MESS INTO DATA(LV_MESSAGE).
          EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = LV_MESSAGE ).
          RETURN.
        ENDLOOP.
      ENDIF.

      " Transform data to output format using modern ABAP
      LT_DATA2 = VALUE #(
        FOR LS_DATA IN LT_DATA
        ( BARCODE   = LS_DATA-BARCODE
          UMREZ     = LS_DATA-UMREZ
          WERKS     = LS_DATA-WERKS
          MATNR     = LS_DATA-MATNR
          LGPLA     = LS_DATA-LGPLA
          VERME     = LS_DATA-QTY
          FLOOR_BIN = LS_DATA-FLOOR_BIN
          ART_TYPE  = LS_DATA-ART_TYPE
        )
      ).

      " Update global variable for backward compatibility with other FMs in function group
      DELETE GT_DATA2 WHERE BARCODE NE IM_BARCODE.
      GT_DATA2 = LT_DATA2.

      " Return successful result
      ET_DATA[] = LT_DATA2[].
      EX_RETURN = VALUE #( TYPE = 'S' MESSAGE = 'Data retrieved successfully' ).

    CATCH CX_SY_SQL_ERROR INTO DATA(LX_SQL).
      EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = |Database error: { LX_SQL->GET_TEXT( ) }| ).
    CATCH CX_ROOT INTO DATA(LX_ROOT).
      EX_RETURN = VALUE #( TYPE = 'E' MESSAGE = |Unexpected error: { LX_ROOT->GET_TEXT( ) }| ).
  ENDTRY.

ENDFUNCTION.