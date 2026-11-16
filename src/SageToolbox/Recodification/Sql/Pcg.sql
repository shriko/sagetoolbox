UPDATE 	F_COMPTEABUDGETG     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_COMPTEGA     SET	CG_Num = @nouveau WHERE 	CG_Num = @ancien
UPDATE F_BANQUE     SET CG_NumFraisOPCVM = @nouveau WHERE CG_NumFraisOPCVM = @ancien
UPDATE F_BANQUE     SET CG_NumTVAOPCVM= @nouveau WHERE CG_NumTVAOPCVM= @ancien
UPDATE F_BANQUE     SET CG_NumMoinsValueOPCVM= @nouveau WHERE CG_NumMoinsValueOPCVM= @ancien
UPDATE F_BANQUE     SET CG_NumPlusValueOPCVM= @nouveau WHERE CG_NumPlusValueOPCVM = @ancien
UPDATE	F_COMPTEGBUDGET     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_COMPTEGBUDGETA     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_COMPTEGNOTE     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE 	F_COMPTET     SET 	CG_NumPrinc = @nouveau WHERE	CG_NumPrinc = @ancien
UPDATE 	F_COMPTETG     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_EBUDGET     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
IF @m_tres = 1 UPDATE F_ECHEANCES     SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_tres = 1 UPDATE F_ECHEANCES     SET CG_NumEff= @nouveau WHERE CG_NumEff = @ancien
IF @m_tres = 1 UPDATE F_ECHEANCES     SET CG_NumTVA = @nouveau WHERE CG_NumTVA = @ancien
IF @m_tres = 1 UPDATE F_ECHEANCES     SET ECH_DernCompte = @nouveau WHERE ECH_DernCompte = @ancien
UPDATE	F_ECRITUREC     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_ECRITUREC     SET 	CG_NumCont = @nouveau WHERE 	CG_NumCont = @ancien
UPDATE	F_ECRITURER     SET	CG_Num = @nouveau  WHERE 	CG_Num = @ancien
UPDATE 	F_ECRITUREF     SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_EFINANCIER     SET
	CG_Num = @nouveau
WHERE
	CG_Num = @ancien
UPDATE
	F_EFINANCIER
    SET
	CG_NumCont = @nouveau
WHERE
	CG_NumCont = @ancien
IF @m_tres = 1 UPDATE
	F_ELOTS
    SET
	CG_Num = @nouveau
WHERE
	CG_Num = @ancien
UPDATE
	F_EMODELEG
    SET
	CX_Num = @nouveau
WHERE
	CX_Num = @ancien AND N_Analytique = 0
IF @m_tres = 1 UPDATE F_ENTETES     SET ENT_CpteEffetALEsc = @nouveau WHERE ENT_CpteEffetALEsc = @ancien
IF @m_tres = 1 UPDATE F_ENTETES     SET ENT_CpteEffetAEsc = @nouveau WHERE ENT_CpteEffetAEsc = @ancien
IF @m_tres = 1 UPDATE F_ERELEVEOPECART     SET EROR_EcartsCG_NumHTEsc = @nouveau WHERE EROR_EcartsCG_NumHTEsc = @ancien
IF @m_tres = 1 UPDATE F_ERELEVEOPECART     SET EROR_EcartsCG_NumAutreEsc = @nouveau WHERE EROR_EcartsCG_NumAutreEsc = @ancien
IF @m_tres = 1 UPDATE F_ERELEVEOPECART     SET EROR_EcartsCG_NumHTAutreEcart = @nouveau WHERE EROR_EcartsCG_NumHTAutreEcart = @ancien
IF @m_tres = 1 UPDATE F_ERELEVEOPECART     SET EROR_EcartsCG_NumAutreAutreEcart = @nouveau WHERE EROR_EcartsCG_NumAutreAutreEcart = @ancien
IF @m_tres = 1 UPDATE F_ESCOMPTES     SET CG_NumHT = @nouveau WHERE CG_NumHT = @ancien
IF @m_tres = 1 UPDATE F_ESCOMPTES     SET CG_NumAutre = @nouveau WHERE CG_NumAutre = @ancien
UPDATE	F_ETAXE    SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_FOURCHETTERESULTAT    SET	CG_NumDe = @nouveau WHERE	CG_NumDe = @ancien
UPDATE	F_FOURCHETTERESULTAT    SET	CG_NumA = @nouveau WHERE	CG_NumA = @ancien
UPDATE	F_JOURNAUX    SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
IF @m_tres = 1 UPDATE	F_LRELEVELCR    SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
IF @m_tres = 1 UPDATE F_MODELEAT     SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_tres = 1 UPDATE F_NATFINANCEMENT     SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_tres = 1 UPDATE F_NATPLACEMENT     SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_tres = 1 UPDATE F_OPCVM     SET CG_Num = @nouveau WHERE CG_Num = @ancien
UPDATE	F_PIECEG    SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	F_PIECEG    SET	CG_NumCont = @nouveau WHERE	CG_NumCont = @ancien
IF @m_tres = 1 UPDATE 	F_PREVTRESO     SET 	CG_Num = @nouveau WHERE 	CG_Num = @ancien
UPDATE F_REGTAXE     SET CG_Num01 = @nouveau WHERE CG_Num01 = @ancien
UPDATE F_REGTAXE     SET CG_Num02 = @nouveau WHERE CG_Num02 = @ancien
UPDATE F_REGTAXE     SET CG_Num03 = @nouveau WHERE CG_Num03 = @ancien
UPDATE F_REGTAXE     SET CG_Num04 = @nouveau WHERE CG_Num04 = @ancien
UPDATE F_REGTAXE     SET CG_Num05 = @nouveau WHERE CG_Num05 = @ancien
UPDATE F_REGTAXE     SET CG_Num06 = @nouveau WHERE CG_Num06 = @ancien
UPDATE F_REGTAXE     SET CG_Num07 = @nouveau WHERE CG_Num07 = @ancien
UPDATE F_REGTAXE     SET CG_Num08 = @nouveau WHERE CG_Num08 = @ancien
UPDATE F_REGTAXE     SET CG_Num09 = @nouveau WHERE CG_Num09 = @ancien
UPDATE F_REGTAXE     SET CG_Num10 = @nouveau WHERE CG_Num10 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num01 = @nouveau WHERE CG_Num01 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num02 = @nouveau WHERE CG_Num02 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num03 = @nouveau WHERE CG_Num03 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num04 = @nouveau WHERE CG_Num04 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num05 = @nouveau WHERE CG_Num05 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num06 = @nouveau WHERE CG_Num06 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num07 = @nouveau WHERE CG_Num07 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num08 = @nouveau WHERE CG_Num08 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num09 = @nouveau WHERE CG_Num09 = @ancien
IF @m_tres = 1 UPDATE F_REGTAXEMDP     SET CG_Num10 = @nouveau WHERE CG_Num10 = @ancien
UPDATE	F_TAXE    SET	CG_Num = @nouveau WHERE	CG_Num = @ancien
UPDATE	P_DOSSIER    SET	CG_NumANOuv = @nouveau WHERE	CG_NumANOuv = @ancien
UPDATE	P_DOSSIER    SET	CG_NumANBenef = @nouveau WHERE	CG_NumANBenef = @ancien
UPDATE	P_DOSSIER    SET	CG_NumANPerte = @nouveau WHERE	CG_NumANPerte = @ancien
UPDATE	P_DOSSIER    SET	CG_NumImportDebit = @nouveau WHERE	CG_NumImportDebit = @ancien
UPDATE	P_DOSSIER    SET	CG_NumImportCredit = @nouveau WHERE	CG_NumImportCredit = @ancien
UPDATE	P_DOSSIER    SET	CG_NumRappro = @nouveau WHERE	CG_NumRappro = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumFraisOPCVM = @nouveau WHERE CG_NumFraisOPCVM = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumTVAOPCVM = @nouveau WHERE CG_NumTVAOPCVM = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumMoinsValueOPCVM = @nouveau WHERE CG_NumMoinsValueOPCVM = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumPlusValueOPCVM = @nouveau WHERE CG_NumPlusValueOPCVM = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumFraisFIN = @nouveau WHERE CG_NumFraisFIN = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumTVAFIN = @nouveau WHERE CG_NumTVAFIN = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumInteretFIN = @nouveau WHERE CG_NumInteretFIN = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumFraisPLA = @nouveau WHERE CG_NumFraisPLA = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumTVAPLA = @nouveau WHERE CG_NumTVAPLA = @ancien
IF @m_tres = 1 UPDATE P_DOSSIERFTRS     SET CG_NumInteretPLA = @nouveau WHERE CG_NumInteretPLA = @ancien
UPDATE P_FISCAL     SET CG_NumMAJDecaisser = @nouveau WHERE CG_NumMAJDecaisser = @ancien
UPDATE P_FISCAL     SET CG_NumMAJCredit = @nouveau  WHERE CG_NumMAJCredit = @ancien
UPDATE P_FISCAL     SET CG_NumMAJEcartD = @nouveau WHERE CG_NumMAJEcartD = @ancien
UPDATE P_FISCAL     SET CG_NumMAJEcartC = @nouveau WHERE CG_NumMAJEcartC = @ancien
UPDATE P_FISCAL     SET CG_NumMAJApur = @nouveau WHERE CG_NumMAJApur = @ancien
UPDATE P_FISCAL     SET F_MAJConstante01CG_NumMAJConstante = @nouveau WHERE F_MAJConstante01CG_NumMAJConstante = @ancien
UPDATE P_FISCAL     SET F_MAJConstante02CG_NumMAJConstante = @nouveau WHERE F_MAJConstante02CG_NumMAJConstante = @ancien
UPDATE P_FISCAL     SET F_MAJConstante03CG_NumMAJConstante = @nouveau WHERE F_MAJConstante03CG_NumMAJConstante = @ancien
UPDATE P_FISCAL     SET F_MAJConstante04CG_NumMAJConstante = @nouveau WHERE F_MAJConstante04CG_NumMAJConstante = @ancien
UPDATE P_FISCAL     SET F_MAJConstante05CG_NumMAJConstante = @nouveau WHERE F_MAJConstante05CG_NumMAJConstante = @ancien
UPDATE P_FISCAL     SET F_MAJConstante06CG_NumMAJConstante = @nouveau WHERE F_MAJConstante06CG_NumMAJConstante = @ancien
UPDATE P_RECOUVREMENT     SET CG_NumPerte = @nouveau WHERE CG_NumPerte = @ancien
UPDATE P_RECOUVREMENT     SET CG_NumDouteux = @nouveau WHERE CG_NumDouteux= @ancien
UPDATE	P_RESULTAT     SET	CG_NumResultat = @nouveau WHERE	CG_NumResultat = @ancien
UPDATE	P_RESULTAT     SET	CG_NumImposition = @nouveau WHERE	CG_NumImposition = @ancien
UPDATE	P_RESULTAT     SET	CG_NumContrePartie = @nouveau WHERE	CG_NumContrePartie = @ancien
UPDATE P_TIERS     SET T_Val01T_Compte = @nouveau WHERE T_Val01T_Compte = @ancien
UPDATE P_TIERS     SET T_Val02T_Compte = @nouveau WHERE T_Val02T_Compte = @ancien
UPDATE P_TIERS     SET T_Val03T_Compte = @nouveau WHERE T_Val03T_Compte = @ancien
UPDATE P_TIERS     SET T_Val04T_Compte = @nouveau WHERE T_Val04T_Compte = @ancien
UPDATE P_TIERS     SET T_Val05T_Compte = @nouveau WHERE T_Val05T_Compte = @ancien
UPDATE P_TIERS     SET T_Val06T_Compte = @nouveau WHERE T_Val06T_Compte = @ancien
UPDATE P_TIERS     SET T_Val07T_Compte = @nouveau WHERE T_Val07T_Compte = @ancien
UPDATE P_TIERS     SET T_Val08T_Compte = @nouveau WHERE T_Val08T_Compte = @ancien
UPDATE P_TIERS     SET T_Val09T_Compte = @nouveau WHERE T_Val09T_Compte = @ancien
UPDATE P_TIERS     SET T_Val10T_Compte = @nouveau WHERE T_Val10T_Compte = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_CpteCharge = @nouveau WHERE D_CpteCharge = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_CpteProduit  = @nouveau WHERE D_CpteProduit = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_CpteEffetALEsc = @nouveau WHERE D_CpteEffetALEsc = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_CpteEffetAEsc = @nouveau WHERE D_CpteEffetAEsc = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_Escompte01D_CompteHTEsc = @nouveau WHERE D_Escompte01D_CompteHTEsc = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_Escompte02D_CompteHTEsc = @nouveau WHERE D_Escompte02D_CompteHTEsc = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_Escompte01D_AutreCompteEsc = @nouveau WHERE D_Escompte01D_AutreCompteEsc = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET D_Escompte02D_AutreCompteEsc = @nouveau WHERE D_Escompte02D_AutreCompteEsc = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER     SET CG_NumDebitChange  = @nouveau WHERE CG_NumDebitChange  = @ancien
IF @m_tres = 1 UPDATE P_TFRAIS     SET CG_NumFraisBancaires = @nouveau WHERE CG_NumFraisBancaires = @ancien
IF @m_tres = 1 UPDATE P_TFRAIS     SET CG_NumTvaFraisBancaires = @nouveau WHERE CG_NumTvaFraisBancaires = @ancien
IF @m_tres = 1 UPDATE P_TFRAIS     SET CG_NumFraisImpayes = @nouveau WHERE CG_NumFraisImpayes = @ancien
IF @m_tres = 1 UPDATE P_TFRAIS     SET CG_NumTvaFraisImpayes = @nouveau WHERE CG_NumTvaFraisImpayes = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape01SCH_CpteTVAEnc = @nouveau WHERE SCH_Etape01SCH_CpteTVAEnc = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape02SCH_CpteTVAEnc = @nouveau WHERE SCH_Etape02SCH_CpteTVAEnc = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape03SCH_CpteTVAEnc = @nouveau WHERE SCH_Etape03SCH_CpteTVAEnc = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape04SCH_CpteTVAEnc = @nouveau WHERE SCH_Etape04SCH_CpteTVAEnc = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape01CG_NumVirInterne = @nouveau WHERE SCH_Etape01CG_NumVirInterne = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape02CG_NumVirInterne = @nouveau WHERE SCH_Etape02CG_NumVirInterne = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape03CG_NumVirInterne = @nouveau WHERE SCH_Etape03CG_NumVirInterne = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape04CG_NumVirInterne = @nouveau WHERE SCH_Etape04CG_NumVirInterne = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape01CG_NumVirIntraGrp = @nouveau WHERE SCH_Etape01CG_NumVirIntraGrp = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape02CG_NumVirIntraGrp = @nouveau WHERE SCH_Etape02CG_NumVirIntraGrp = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape03CG_NumVirIntraGrp = @nouveau WHERE SCH_Etape03CG_NumVirIntraGrp = @ancien
IF @m_tres = 1 UPDATE	P_TSCHEMAS     SET SCH_Etape04CG_NumVirIntraGrp = @nouveau WHERE SCH_Etape04CG_NumVirIntraGrp = @ancien
IF @m_cial = 1 UPDATE F_ABOENTETE SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_cial = 1 UPDATE F_DOCENTETE SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_cial = 1 UPDATE F_CREGLEMENT SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_cial = 1 UPDATE F_CREGLEMENT SET CG_NumCont = @nouveau WHERE CG_NumCont = @ancien
IF @m_cial = 1 UPDATE F_CREGLEMENT SET CG_NumEcart = @nouveau WHERE CG_NumEcart = @ancien
IF @m_tres = 1 UPDATE F_ECARTREGLEMENT SET ECTREG_EcartsCG_NumHTEsc = @nouveau WHERE ECTREG_EcartsCG_NumHTEsc = @ancien
IF @m_tres = 1 UPDATE F_ECARTREGLEMENT SET ECTREG_EcartsCG_NumAutreEsc = @nouveau WHERE ECTREG_EcartsCG_NumAutreEsc = @ancien
IF @m_tres = 1 UPDATE F_ECARTREGLEMENT SET ECTREG_EcartsCG_NumHTAutreEcart = @nouveau WHERE ECTREG_EcartsCG_NumHTAutreEcart = @ancien
IF @m_tres = 1 UPDATE F_ECARTREGLEMENT SET ECTREG_EcartsCG_NumAutreAutreEcart = @nouveau WHERE ECTREG_EcartsCG_NumAutreAutreEcart = @ancien
IF @m_tres = 1 UPDATE F_ERELEVEOP SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET CG_Num = @nouveau WHERE CG_Num = @ancien
IF @m_cial = 1 UPDATE F_ARTCOMPTA SET ACP_ComptaCpt_CompteG = @nouveau WHERE ACP_ComptaCpt_CompteG = @ancien
IF @m_cial = 1 UPDATE F_FAMCOMPTA SET FCP_ComptaCpt_CompteG = @nouveau WHERE FCP_ComptaCpt_CompteG = @ancien
IF @m_cial = 1 UPDATE P_PREFERENCES SET CG_NumCli = @nouveau WHERE CG_NumCli = @ancien
IF @m_cial = 1 UPDATE P_PREFERENCES SET CG_NumFrs = @nouveau WHERE CG_NumFrs = @ancien
IF @m_cial = 1 UPDATE P_PREFERENCES SET CG_NumVirement = @nouveau WHERE CG_NumVirement = @ancien
IF @m_cial = 1 UPDATE P_PREFERENCES SET CG_NumComptoirDebit = @nouveau WHERE CG_NumComptoirDebit = @ancien
IF @m_cial = 1 UPDATE P_PREFERENCES SET CG_NumComptoirCredit = @nouveau WHERE CG_NumComptoirCredit = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER SET CG_NumCreditChange = @nouveau WHERE CG_NumCreditChange = @ancien
UPDATE P_ESERVICES SET ES_PaymentsCG_NumStripe = @nouveau WHERE ES_PaymentsCG_NumStripe = @ancien
UPDATE P_ESERVICES SET ES_PaymentsCG_NumPaypal = @nouveau WHERE ES_PaymentsCG_NumPaypal = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte01 = @nouveau WHERE IM_Compte01 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte02 = @nouveau WHERE IM_Compte02 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte03 = @nouveau WHERE IM_Compte03 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte04 = @nouveau WHERE IM_Compte04 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte05 = @nouveau WHERE IM_Compte05 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte06 = @nouveau WHERE IM_Compte06 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte07 = @nouveau WHERE IM_Compte07 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte08 = @nouveau WHERE IM_Compte08 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte09 = @nouveau WHERE IM_Compte09 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte10 = @nouveau WHERE IM_Compte10 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte11 = @nouveau WHERE IM_Compte11 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte12 = @nouveau WHERE IM_Compte12 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte13 = @nouveau WHERE IM_Compte13 = @ancien
IF @m_immo = 1 UPDATE F_IMMOBILISATION SET IM_Compte14 = @nouveau WHERE IM_Compte14 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte01 = @nouveau WHERE FA_Compte01 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte02 = @nouveau WHERE FA_Compte02 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte03 = @nouveau WHERE FA_Compte03 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte04 = @nouveau WHERE FA_Compte04 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte05 = @nouveau WHERE FA_Compte05 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte06 = @nouveau WHERE FA_Compte06 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte07 = @nouveau WHERE FA_Compte07 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte08 = @nouveau WHERE FA_Compte08 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte09 = @nouveau WHERE FA_Compte09 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte10 = @nouveau WHERE FA_Compte10 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte11 = @nouveau WHERE FA_Compte11 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte12 = @nouveau WHERE FA_Compte12 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte13 = @nouveau WHERE FA_Compte13 = @ancien
IF @m_immo = 1 UPDATE F_FAMILLEIMMO SET FA_Compte14 = @nouveau WHERE FA_Compte14 = @ancien
UPDATE	F_COMPTEG     SET 	CG_Num = @nouveau WHERE 	CG_Num = @ancien
