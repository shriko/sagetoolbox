IF @m_cial = 1 UPDATE F_ABOENTETE SET CA_Num = @nouveau WHERE CA_Num = @ancien	AND	EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnalDoc = 1)
IF @m_cial = 1 UPDATE F_ABOLIGNE SET CA_Num = @nouveau WHERE CA_Num = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnalDoc = 1)
IF @m_cial = 1 UPDATE F_ARTCOMPTA SET ACP_ComptaCpt_CompteA = @nouveau WHERE ACP_ComptaCpt_CompteA = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnal = 1)
UPDATE F_BUDGET SET BD_VRepart = @nouveau WHERE BD_VRepart = @ancien AND BD_TRepart = 1
UPDATE F_COMPTEABUDGET SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
UPDATE F_COMPTEABUDGETG SET	CA_Num = @nouveau WHERE	CA_Num = @ancien AND N_Analytique = @plan
UPDATE F_COMPTEGA SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
UPDATE F_COMPTEGBUDGETA SET	CA_Num = @nouveau WHERE	CA_Num = @ancien AND N_Analytique = @plan
UPDATE F_COMPTET SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
IF @m_cial = 1 UPDATE F_DOCENTETE SET CA_Num = @nouveau WHERE CA_Num = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnalDoc = 1)
IF @m_cial = 1 UPDATE F_DOCLIGNE SET CA_Num = @nouveau WHERE CA_Num = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnalDoc = 1)
UPDATE F_ECRITUREA SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
UPDATE F_ECRITURER SET CA_Num = @nouveau WHERE CA_Num = @ancien	AND N_Analytique = @plan
UPDATE F_EMODELEG SET CX_Num = @nouveau WHERE N_Analytique = @plan AND CX_Num = @ancien
UPDATE F_EFINANCIERA SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
IF @m_immo = 1 UPDATE F_FAMANAL SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
IF @m_cial = 1 UPDATE F_FAMCOMPTA SET FCP_ComptaCpt_CompteA = @nouveau WHERE FCP_ComptaCpt_CompteA = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnal = 1)
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET CA_Num = @nouveau WHERE CA_Num = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETREIMMO WHERE P_ChpAnalImmo = 1)
IF @m_immo = 1 UPDATE F_IMMOANAL SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET CA_Num = @nouveau WHERE CA_Num = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETREIMMO WHERE P_ChpAnalImmo = 1)
UPDATE F_PIECEA SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
IF @m_cial = 1 UPDATE F_PROJETFABRICATION SET CA_Num = @nouveau WHERE CA_Num = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnalDoc = 1)
UPDATE P_ANALYTIQUE SET CA_Num = @nouveau WHERE CA_Num = @ancien AND cbIndice = @plan
UPDATE F_COMPTEAMEDIA SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
UPDATE F_CONTACTA SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
UPDATE F_COMPTET SET CA_NumIFRS = @nouveau WHERE CA_NumIFRS = @ancien AND N_AnalytiqueIFRS = @plan
IF @m_cial = 1 UPDATE F_DOCENTETE SET CA_NumIFRS = @nouveau WHERE CA_NumIFRS = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnalDoc = 1)
IF @m_cial = 1 UPDATE F_ABOENTETE SET CA_NumIFRS = @nouveau WHERE CA_NumIFRS = @ancien AND EXISTS(SELECT 1 FROM P_PARAMETRECIAL WHERE P_ChpAnalDoc = 1)
UPDATE F_COMPTEA SET CA_Num = @nouveau WHERE CA_Num = @ancien AND N_Analytique = @plan
