# Journal des modifications

Les versions sont publiées sur [GitHub](https://github.com/shriko/sagetoolbox/releases). Au démarrage, l'application propose d'installer la dernière version disponible.

## [1.3.0] — 2026-11-16

### Nouveautés
- **Recodification (bêta)** : changement en masse des codes des comptes généraux, tiers, journaux, comptes analytiques, familles et articles (saisie ou import / export Excel), contrôles des nouveaux codes, fusion dans une fiche existante, traitement SQL transactionnel avec contrôles préalables (utilisateurs connectés, sauvegarde) et rapport HTML. Compatible SPC : tables préfixées du schéma de la société, sessions et sauvegarde confirmées par l'utilisateur.
- **Connexion SPC (Sage Premium Cloud)** : connexion à un serveur Azure SQL (adresse, base, utilisateur et mot de passe SQL), lecture des sociétés (`cbase.companies`) et sélection de la société de travail. Seul le schéma SQL change d'une société à l'autre (`soc_bijou.F_ECRITUREC` au lieu de `dbo.F_ECRITUREC`). Les outils indisponibles en SPC (sauvegarde, restauration, copie des utilisateurs, optimisation, fichiers de connexion, mot de passe administrateur, utilisateurs fantômes) sont grisés ; la déclôture n'y propose pas de sauvegarde préalable ; l'Audit TVA liste les sociétés du dossier.

## [1.1.0] — 2026-11-14

### Nouveautés
- **Audit TVA** : audit en lecture seule du paramétrage et de l'usage de la TVA sur les bases Sage Comptabilité de l'instance (10 contrôles par base), avec rapport **Word, PDF et/ou Excel** (un rapport global ou un par base).
- **Conformité factures** : contrôle d'une facture électronique XML UBL / CII / Factur-X / CDAR, ou d'un PDF Factur-X, contre les schematrons officiels **France_RFE (FNFE) 1.4.0.04**. Détection automatique du profil, rapport règle par règle, contrôle de structure XSD et points de vigilance. Fonctionne sans connexion à une base.
- **Listing RFE** : liste des adresses de facturation électronique (« maillage » SIREN, SIRET, code routage, suffixe) des clients actifs, avec contacts, filtres, choix des colonnes et export Excel.
- **Enrichissement tiers** : fiabilisation des SIRET / SIREN des clients et fournisseurs via l'API « Recherche d'entreprises » (INSEE / Sirene), choix manuel des candidats, export Excel et mise à jour de Sage 100 (SIRET, nom, adresse, n° de TVA, mise en sommeil des tiers radiés) avec journal d'audit avant / après.
- **Dévalidation de factures par liste** : factures de vente validées non comptabilisées (n°, date, client, montant HT), sélection multiple, filtres par date, client et n° de facture. Une facture déjà transmise en facturation électronique n'est pas dévalidable (elle existe côté Administration Fiscale).

### Interface
- Nouvelle interface en **tuiles** (icône et titre), regroupées par nature d'action : Contrôle & audit, Correction de données, Utilisateurs & sécurité, Sauvegarde & restauration, Maintenance technique.
- Colonne de gauche toujours visible : connexion, informations sur la base et l'instance, thème.
- Les outils à saisie s'ouvrent dans la zone centrale avec un bouton « ← Retour » (les fenêtres modales disparaissent).
- **Optimiser** et **Fichiers de connexion** fusionnent leurs variantes « une base » et « toutes les bases » : un choix de portée est proposé.
- Badge **Instance** sur les outils qui n'exigent que la connexion à l'instance ; liseré rouge sur les outils sensibles (déclôture, dévalidation, mot de passe administrateur, restauration).

### Modifications
- **Déclôture d'exercice** : liste lisible des exercices avec cadenas fermé (clôturé) ou ouvert, et case de confirmation obligatoire à la place de la boîte de dialogue finale.
- Documentation (README et guide utilisateur) mise à jour ; documents regroupés dans `docs/`.
- L'exécutable grossit d'environ 45 Mo (règles FNFE, moteur Saxon-HE, génération des rapports) : environ 195 Mo.

### Technique
- Version Python d'origine retirée du dépôt.
- Dépendances ajoutées : SaxonCS-HE (MPL 2.0), PdfPig, ClosedXML, DocumentFormat.OpenXml, QuestPDF (licence Community). Règles France_RFE sous licence Apache 2.0 embarquées dans l'exécutable.

## [1.0.0] — 2026-10-28

- Migration du projet vers **.NET 10**.
- **Mise à jour automatique** : au démarrage, l'application détecte une nouvelle release GitHub, propose de l'installer, la télécharge et se relance.
- Publication des versions par workflow GitHub Actions à partir d'un tag `vX.Y.Z`.
