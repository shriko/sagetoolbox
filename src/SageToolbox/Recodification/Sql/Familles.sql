UPDATE F_ARTICLE SET FA_CodeFamille = @nouveau WHERE FA_CodeFamille = @ancien
UPDATE F_COMMPLANIFIE SET FA_CodeFamilleDe = @nouveau WHERE FA_CodeFamilleDe = @ancien
UPDATE F_COMMPLANIFIE SET FA_CodeFamilleA = @nouveau WHERE FA_CodeFamilleA = @ancien
UPDATE F_FAMCLIENT SET FA_CodeFamille = @nouveau WHERE FA_CodeFamille = @ancien
UPDATE F_FAMCOMPTA SET FA_CodeFamille = @nouveau WHERE FA_CodeFamille = @ancien
UPDATE F_FAMFOURNISS SET FA_CodeFamille = @nouveau WHERE FA_CodeFamille = @ancien
UPDATE F_FAMMODELE SET FA_CodeFamille = @nouveau WHERE FA_CodeFamille = @ancien
UPDATE F_FAMTARIF SET FA_CodeFamille = @nouveau WHERE FA_CodeFamille = @ancien
UPDATE F_FAMTARIFQTE SET FA_CodeFamille = @nouveau WHERE FA_CodeFamille = @ancien
UPDATE F_TARIFSELECT SET TS_Ref = @nouveau WHERE TS_Interes = 0 AND TS_Ref = @ancien
UPDATE F_FAMILLE SET FA_Central = @nouveau WHERE FA_Type = 0 AND FA_Central = @ancien
UPDATE F_FAMILLE SET FA_CodeFamille = @nouveau WHERE FA_Type = 0 AND FA_CodeFamille = @ancien
