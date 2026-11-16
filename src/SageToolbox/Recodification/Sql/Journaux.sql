IF @m_cial = 1 UPDATE F_CAISSE SET JO_Num = @nouveau WHERE JO_Num = @ancien
IF @m_cial = 1 UPDATE F_CREGLEMENT SET JO_Num = @nouveau WHERE JO_Num = @ancien
IF @m_cial = 1 UPDATE F_CREGLEMENT SET JO_NumEcart = @nouveau WHERE JO_NumEcart = @ancien
UPDATE F_CTRLFIELDECRITURECJNL SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE F_EBANQUE SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE F_ECRITUREC SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE F_EFINANCIEREC SET JO_NumAutre = @nouveau WHERE JO_NumAutre = @ancien
IF @m_tres = 1 UPDATE F_ESCOMPTES SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE F_FINANCIER SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE F_JMOUV SET JO_Num = @nouveau  WHERE JO_Num = @ancien
UPDATE F_MODELEA SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE F_PIECE SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE	P_DOSSIER SET JO_NumAN = @nouveau WHERE JO_NumAN = @ancien
UPDATE	P_DOSSIER SET JO_NumRapproEscCl = @nouveau WHERE JO_NumRapproEscCl = @ancien
UPDATE	P_DOSSIER SET JO_NumRapproEscFr = @nouveau WHERE JO_NumRapproEscFr = @ancien
UPDATE	P_DOSSIER SET JO_NumANIFRS = @nouveau WHERE JO_NumANIFRS = @ancien
UPDATE	P_DOSSIER SET JO_NumPenal = @nouveau WHERE JO_NumPenal = @ancien
UPDATE	P_DOSSIER SET JO_NumImpayes = @nouveau WHERE JO_NumImpayes = @ancien
UPDATE P_FISCAL SET JO_NumMAJ = @nouveau WHERE JO_NumMAJ = @ancien
UPDATE P_LETTRAGE SET JO_Num = @nouveau WHERE JO_Num = @ancien
UPDATE P_LETTRAGE SET JO_NumConv = @nouveau WHERE JO_NumConv = @ancien
UPDATE P_LETTRAGE SET JO_NumChange = @nouveau WHERE JO_NumChange = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_Journal01 = @nouveau WHERE P_Journal01 = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_Journal02 = @nouveau WHERE P_Journal02 = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_Journal03 = @nouveau WHERE P_Journal03 = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_Journal04 = @nouveau WHERE P_Journal04 = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_Journal05 = @nouveau WHERE P_Journal05 = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_Journal06 = @nouveau WHERE P_Journal06 = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_JournalCaisse = @nouveau WHERE P_JournalCaisse = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_JournEcartRegl = @nouveau WHERE P_JournEcartRegl = @ancien
IF @m_cial = 1 UPDATE P_PARAMETRECIAL SET P_JournEcartConv = @nouveau WHERE P_JournEcartConv = @ancien
UPDATE P_RECOUVREMENT SET JO_NumProv = @nouveau WHERE JO_NumProv = @ancien
IF @m_immo = 1 UPDATE P_REGLAGES SET JO_NumExercice = @nouveau WHERE JO_NumExercice = @ancien
IF @m_immo = 1 UPDATE P_REGLAGES SET JO_NumSituation = @nouveau WHERE JO_NumSituation = @ancien
IF @m_immo = 1 UPDATE P_REGLAGES SET JO_NumExerciceIFRS = @nouveau WHERE JO_NumExerciceIFRS = @ancien
IF @m_immo = 1 UPDATE P_REGLAGES SET JO_NumSituationIFRS = @nouveau WHERE JO_NumSituationIFRS = @ancien
IF @m_cial = 1 UPDATE P_SCDREGLEMENT SET JO_NumCli = @nouveau WHERE JO_NumCli = @ancien
IF @m_cial = 1 UPDATE P_SCDREGLEMENT SET JO_NumFour = @nouveau WHERE JO_NumFour = @ancien
IF @m_cial = 1 UPDATE P_SOUCHEACHAT SET JO_Num = @nouveau WHERE JO_Num = @ancien
IF @m_cial = 1 UPDATE P_SOUCHEACHAT SET JO_NumSituation = @nouveau WHERE JO_NumSituation = @ancien
IF @m_cial = 1 UPDATE P_SOUCHEVENTE SET JO_Num = @nouveau WHERE JO_Num = @ancien
IF @m_cial = 1 UPDATE P_SOUCHEVENTE SET JO_NumSituation = @nouveau WHERE JO_NumSituation = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER SET D_CodeJournal = @nouveau WHERE D_CodeJournal = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER SET D_Escompte01D_CodeJournalEsc = @nouveau WHERE D_Escompte01D_CodeJournalEsc = @ancien
IF @m_tres = 1 UPDATE P_TDOSSIER SET D_Escompte02D_CodeJournalEsc = @nouveau WHERE D_Escompte02D_CodeJournalEsc = @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape01JO_Num01= @nouveau WHERE SCH_Etape01JO_Num01= @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape01JO_Num02= @nouveau WHERE SCH_Etape01JO_Num02= @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape02JO_Num01= @nouveau WHERE SCH_Etape02JO_Num01= @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape02JO_Num02= @nouveau WHERE SCH_Etape02JO_Num02= @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape03JO_Num01= @nouveau WHERE SCH_Etape03JO_Num01= @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape03JO_Num02= @nouveau WHERE SCH_Etape03JO_Num02= @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape04JO_Num01= @nouveau WHERE SCH_Etape04JO_Num01= @ancien
IF @m_tres = 1 UPDATE P_TSCHEMAS SET SCH_Etape04JO_Num02= @nouveau WHERE SCH_Etape04JO_Num02= @ancien
UPDATE P_REGULR SET JO_Num=@nouveau WHERE JO_Num= @ancien
IF @m_tres = 1 UPDATE P_TFRAIS SET JO_NumFraisImpayes = @nouveau WHERE JO_NumFraisImpayes = @ancien
UPDATE F_EBANQUE SET JO_NumEscompte = @nouveau WHERE JO_NumEscompte = @ancien
UPDATE F_EBANQUE SET JO_NumEncaiss = @nouveau WHERE JO_NumEncaiss = @ancien
UPDATE P_ESERVICES SET ES_EdifactureJO_Num = @nouveau WHERE ES_EdifactureJO_Num = @ancien
UPDATE P_ESERVICES SET ES_PaymentsJO_NumStripe = @nouveau WHERE ES_PaymentsJO_NumStripe = @ancien
UPDATE P_ESERVICES SET ES_PaymentsJO_NumPaypal = @nouveau WHERE ES_PaymentsJO_NumPaypal = @ancien
UPDATE F_JOURNAUX SET JO_Num = @nouveau WHERE JO_Num = @ancien
