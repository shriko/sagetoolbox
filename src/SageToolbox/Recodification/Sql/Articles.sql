UPDATE P_COMMUNICATION SET AR_RefAttente = @nouveau WHERE AR_RefAttente = @ancien
UPDATE P_EXPEDITION SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE P_PREFERENCES SET PR_RefEsc = @nouveau WHERE PR_RefEsc = @ancien
UPDATE P_PREFERENCES SET PR_RefTaxeNP = @nouveau WHERE PR_RefTaxeNP = @ancien
UPDATE F_AGENDA SET AG_Interes = @nouveau WHERE AG_Domaine = 4 AND AG_Interes = @ancien
UPDATE F_ABOLIGNE
SET
	AR_Ref = CASE WHEN AR_Ref = @ancien THEN @nouveau ELSE AR_Ref END,
	AR_RefCompose = CASE WHEN AR_RefCompose = @ancien THEN @nouveau ELSE AR_RefCompose END
WHERE
	AR_Ref = @ancien OR AR_RefCompose = @ancien
UPDATE F_ARTCLIENT SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTCOMPO SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTCOMPTA SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTENUMREF SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTFOURNISS SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTGAMME SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTGLOSS SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTICLEMEDIA SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTICLERESSOURCE SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTMODELE SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTPRIX SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTSTOCK SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_ARTSTOCKEMPL SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_CLAVIER SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_COMMPLANIFIE SET AR_RefDe = @nouveau WHERE AR_RefDe = @ancien
UPDATE F_COMMPLANIFIE SET AR_RefA = @nouveau WHERE AR_RefA = @ancien
UPDATE F_CONDITION SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_DOCLIGNE
SET
	AR_Ref = CASE WHEN AR_Ref = @ancien THEN @nouveau ELSE AR_Ref END,
	AR_RefCompose = CASE WHEN AR_RefCompose = @ancien THEN @nouveau ELSE AR_RefCompose END
WHERE
	AR_Ref = @ancien OR AR_RefCompose = @ancien
UPDATE F_GAMSTOCK SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_GAMSTOCKEMPL SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_LIGNEARCHIVE SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_LOTFIFO SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_LOTSERIE SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_NOMENCLAT
SET
	AR_Ref = CASE WHEN AR_Ref = @ancien THEN @nouveau ELSE AR_Ref END,
	NO_RefDet = CASE WHEN NO_RefDet = @ancien THEN @nouveau ELSE NO_RefDet END
WHERE
	AR_Ref = @ancien OR NO_RefDet = @ancien
UPDATE F_PREVISION SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_PROJETHISTO SET AR_RefCompose = @nouveau WHERE AR_RefCompose = @ancien
UPDATE F_PROJETHISTO SET AR_RefComposant = @nouveau WHERE AR_RefComposant = @ancien
UPDATE F_PROJETPLANNING SET AR_RefCompose = @nouveau WHERE AR_RefCompose = @ancien
UPDATE F_PROJETPLANNING SET AR_RefComposant = @nouveau WHERE AR_RefComposant = @ancien
UPDATE F_RESSOURCEPROD SET AR_RefDefaut = @nouveau WHERE AR_RefDefaut = @ancien
UPDATE F_TARIF SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_TARIFCOND SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_TARIFGAM SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_TARIFQTE SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE F_TARIFSELECT SET TS_Ref = @nouveau WHERE TS_Interes = 1 AND TS_Ref = @ancien
UPDATE P_ECHANGECIAL SET AR_Ref01 = @nouveau WHERE AR_Ref01 =  @ancien
UPDATE P_ECHANGECIAL SET AR_Ref02 = @nouveau WHERE AR_Ref02 =  @ancien
UPDATE P_ECHANGECIAL SET AR_Ref03 = @nouveau WHERE AR_Ref03 =  @ancien
UPDATE P_ECHANGECIAL SET AR_Ref04 = @nouveau WHERE AR_Ref04 =  @ancien
UPDATE P_ECHANGECIAL SET AR_Ref05 = @nouveau WHERE AR_Ref05 =  @ancien
UPDATE P_PARAMETRECIAL SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
UPDATE P_PARAMETRECIAL SET AR_RefAcompte = @nouveau WHERE AR_RefAcompte = @ancien
UPDATE F_ARTICLE SET AR_Substitut = @nouveau WHERE AR_Substitut = @ancien
UPDATE F_ARTICLE SET AR_Ref = @nouveau WHERE AR_Ref = @ancien
