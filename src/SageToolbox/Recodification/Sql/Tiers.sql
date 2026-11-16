IF @m_cial = 1 UPDATE F_AGENDA SET AG_Interes =  @nouveau WHERE AG_Domaine = 1 AND AG_Interes =  @ancien
IF @m_cial = 1 UPDATE F_ABOENTETE SET CT_NumPayeur =  @nouveau WHERE CT_NumPayeur =  @ancien
IF @m_cial = 1 UPDATE F_ABOENTETE SET CT_NumCentrale =  @nouveau WHERE CT_NumCentrale =  @ancien
IF @m_cial = 1 UPDATE F_ABONNEMENT SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
IF @m_cial = 1 UPDATE F_ARTCLIENT SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_BANQUET SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
IF @m_cial = 1 UPDATE F_CAISSE SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
IF @m_cial = 1 UPDATE F_COMMPLANIFIE SET CT_NumDe =  @nouveau WHERE CT_NumDe =  @ancien
IF @m_cial = 1 UPDATE F_COMMPLANIFIE SET CT_NumA =  @nouveau WHERE CT_NumA =  @ancien
UPDATE F_COMPTET
SET
	CT_Num = CASE WHEN CT_Num =  @ancien  THEN  @nouveau ELSE CT_Num END,
	CT_NumPayeur = CASE WHEN CT_NumPayeur =  @ancien  THEN  @nouveau ELSE CT_NumPayeur END
WHERE
	CT_Num =  @ancien  OR CT_NumPayeur =  @ancien
UPDATE F_COMPTET SET CT_NumCentrale =  @nouveau WHERE CT_NumCentrale =  @ancien
UPDATE F_MANDAT SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_COMPTETG SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_COMPTETMEDIA SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_COMPTETMODELE SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_COMPTETRAPPEL SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_CONTACTT SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
IF @m_cial = 1 UPDATE F_CREGLEMENT
SET
	CT_NumPayeur = CASE WHEN CT_NumPayeur =  @ancien  THEN  @nouveau ELSE CT_NumPayeur END,
	CT_NumPayeurOrig = CASE WHEN CT_NumPayeurOrig =  @ancien  THEN  @nouveau ELSE CT_NumPayeurOrig END
WHERE
	CT_NumPayeur =  @ancien  OR CT_NumPayeurOrig =  @ancien
IF @m_cial = 1 UPDATE F_DOCENTETE
SET
	DO_Tiers = CASE WHEN DO_Tiers =  @ancien  AND DO_Domaine != 2 THEN  @nouveau ELSE DO_Tiers END,
	CT_NumPayeur = CASE WHEN CT_NumPayeur =  @ancien  THEN  @nouveau ELSE CT_NumPayeur END,
	CT_NumCentrale = CASE WHEN CT_NumCentrale =  @ancien  THEN  @nouveau ELSE CT_NumCentrale END
WHERE
	(DO_Tiers =  @ancien  AND DO_Domaine != 2) OR CT_NumPayeur =  @ancien  OR CT_NumCentrale =  @ancien
IF @m_cial = 1 UPDATE F_DOCLIGNE SET CT_Num =  @nouveau WHERE CT_Num =  @ancien   AND DO_Domaine != 2
UPDATE F_DRECOUVREMENT SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_ECRITUREC
SET
	CT_Num = CASE WHEN CT_Num =  @ancien  THEN  @nouveau ELSE CT_Num END,
	CT_NumCont = CASE WHEN CT_NumCont =  @ancien  THEN  @nouveau ELSE CT_NumCont END
WHERE
	CT_Num =  @ancien  OR CT_NumCont =  @ancien
UPDATE F_EFINANCIER
SET
	CT_Num = CASE WHEN CT_Num =  @ancien  THEN  @nouveau ELSE CT_Num END,
	CT_NumCont = CASE WHEN CT_NumCont =  @ancien  THEN  @nouveau ELSE CT_NumCont END
WHERE
	CT_Num =  @ancien  OR CT_NumCont =  @ancien
IF @m_cial = 1 UPDATE F_FAMCLIENT SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_LIVRAISON SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_PIECEG
SET
	CT_Num = CASE WHEN CT_Num =  @ancien  THEN  @nouveau ELSE CT_Num END,
	CT_NumCont = CASE WHEN CT_NumCont =  @ancien  THEN  @nouveau ELSE CT_NumCont END
WHERE
	CT_Num =  @ancien  OR CT_NumCont =  @ancien
IF @m_cial = 1 UPDATE F_PROJETFABRICATION SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_REGLEMENTT SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_REGTAXE SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
IF @m_cial = 1 UPDATE F_TARIFCOND SET TC_RefCF =  @nouveau WHERE TC_RefCF =  @ancien
IF @m_cial = 1 UPDATE F_TARIFGAM SET TG_RefCF =  @nouveau WHERE TG_RefCF =  @ancien
IF @m_cial = 1 UPDATE F_TARIFQTE SET TQ_RefCF =  @nouveau WHERE TQ_RefCF =  @ancien
IF @m_cial = 1 UPDATE F_TARIFSELECT SET TS_Ref =  @nouveau WHERE TS_Ref =  @ancien  AND TS_Interes = 3
IF @m_cial = 1 UPDATE F_TICKETARCHIVE SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
IF @m_cial = 1 UPDATE P_PREFERENCES SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
UPDATE F_COMPTETNOTE SET CT_Num =  @nouveau WHERE CT_Num =  @ancien
IF @m_cial = 1 UPDATE F_ARTFOURNISS SET CT_Num = @nouveau WHERE CT_Num = @ancien
UPDATE F_COMPTETFACTRECEPTION SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_cial = 1 UPDATE F_COMPTETINFOS SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_tres = 1 UPDATE F_ECHEANCES SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_tres = 1 UPDATE F_ECHEANCES SET CT_NumPayeur = @nouveau WHERE CT_NumPayeur = @ancien
IF @m_tres = 1 UPDATE F_ELOTS SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_tres = 1 UPDATE F_ERELEVEOP SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_cial = 1 UPDATE F_FAMFOURNISS SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_cial = 1 UPDATE F_FAMTARIFQTE SET FQ_RefCF = @nouveau WHERE FQ_RefCF = @ancien
IF @m_tres = 1 UPDATE F_HISTOMANDATS SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_tres = 1 UPDATE F_LRELEVELCR SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_tres = 1 UPDATE F_MODELEAT SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_tres = 1 UPDATE F_PREVTRESO SET CT_Num = @nouveau WHERE CT_Num = @ancien
IF @m_cial = 1 UPDATE F_TARIF SET CT_Num = @nouveau WHERE CT_Num = @ancien
