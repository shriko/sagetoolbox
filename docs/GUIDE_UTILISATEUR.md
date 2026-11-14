# Guide utilisateur — Sage Toolbox

## Ce à quoi sert l'outil

Sage Toolbox est un outil d'administration pour les bases **Sage 100** (Comptabilité, Gestion Commerciale, Trésorerie, Immobilisations) hébergées sur **SQL Server**. Il rassemble en une seule application les opérations de maintenance, de dépannage et d'administration qu'un technicien effectue habituellement à la main via SQL Server Management Studio ou directement dans Sage — connexion à une base, optimisation, contrôles d'intégrité, déclôture, gestion des utilisateurs, sauvegarde/restauration — avec une interface simple, un journal de chaque action, et des confirmations avant toute opération sensible.

Il se connecte directement aux tables SQL Server de Sage (authentification Windows intégrée, sans identifiant à saisir) — ce n'est pas un module Sage, mais un outil technique destiné aux personnes qui administrent les installations Sage (support, IT).

## Objectifs de l'outil

- **Centraliser** les opérations d'administration Sage courantes dans un seul endroit, sans avoir à écrire de requêtes SQL à la main.
- **Sécuriser** les opérations sensibles (confirmation systématique, journal de toutes les actions, sauvegarde avant les opérations destructives).
- **Gagner du temps** sur les opérations répétitives ou multi-bases (optimiser toutes les bases d'une instance, sauvegarder toutes les bases, copier des utilisateurs entre deux bases, restaurer un lot de sauvegardes).
- **Diagnostiquer** les problèmes d'intégrité d'une base comptable ou de gestion commerciale avant qu'ils ne posent problème aux utilisateurs Sage.

## Prérequis

- **Windows**, avec un compte disposant des droits nécessaires sur l'instance SQL Server ciblée (l'outil se connecte en authentification Windows intégrée — pas de mot de passe SQL à saisir).
- Un accès réseau à l'**instance SQL Server** hébergeant les bases Sage à administrer.
- **Aucune installation requise** : l'outil est livré sous forme d'un seul fichier exécutable (`SageToolbox.exe`), à copier et lancer directement — le runtime nécessaire est inclus dedans.
- Le **mot de passe applicatif** de l'outil (demandé à chaque lancement) doit être connu — c'est le même mot de passe que l'ancien outil AIG Toolbox.

## Fonctionnement

### Démarrage

Au lancement, l'outil demande un **mot de passe applicatif**. Ce mot de passe protège simplement l'accès à l'outil (pas aux bases Sage elles-mêmes) — sans lui, l'application ne s'ouvre pas.

### Se connecter

Trois façons de se connecter, dans la colonne de gauche de la fenêtre principale :

- **Instance + base** : renseigner le nom du serveur/instance SQL (ex. `SRV01\SQLEXPRESS`) et le nom de la base Sage, puis cliquer sur **Connecter**. Toutes les fonctions de l'outil deviennent disponibles.
- **Instance seule** : renseigner uniquement l'instance SQL (laisser "Base de données" vide) puis **Connecter**. Seuls les outils portant le badge **Instance** (qui agissent sur toute l'instance, pas sur une base précise) sont alors activés — utile par exemple pour sauvegarder ou restaurer un lot de bases sans avoir à se connecter à une base en particulier.
- **Ouvrir fichier local (.mae)** : au lieu de saisir l'instance et la base à la main, sélectionner un fichier `.mae` (fichier de connexion Sage existant) — l'outil en extrait automatiquement le serveur et le nom de base et se connecte.

Une fois connecté, le bandeau de gauche affiche les informations de la base (modules Sage installés, taille des fichiers, emplacement) et de l'instance SQL (version, édition, mode d'authentification, état de FILESTREAM, répertoires par défaut).

### Utiliser une fonction

Chaque tuile correspond à un outil. Cliquer dessus lance directement l'action après une **confirmation** (Oui/Non), ou ouvre un **écran dans la zone centrale** si l'outil a besoin d'informations complémentaires (un dossier, une sélection, une case à cocher...). Le bouton **← Retour** en haut ramène à la grille des outils. Pendant qu'une action tourne, les tuiles sont désactivées et le curseur indique une opération en cours.

### Le journal

En bas de la fenêtre, le **journal** affiche en temps réel le déroulé de chaque action (ℹ️ information, ✅ succès, ⚠️ avertissement, ❌ erreur). Il est aussi enregistré dans un fichier sur le poste (`%APPDATA%\SageToolbox\sage_toolbox.log`), consultable après coup. Le **contrôle d'intégrité** génère en plus un fichier `.txt` détaillé à côté de l'exécutable s'il trouve des anomalies.

### Le thème

En bas du bandeau de gauche, un sélecteur permet de choisir l'apparence : **Système** (suit le réglage Windows), **Clair** ou **Sombre**.

---

## Détail des outils

Les outils sont présentés en tuiles dans la zone centrale, regroupés par nature d'action. Un badge **Instance** signale les outils qui n'ont besoin que de la connexion à l'instance ; un liseré **rouge** signale les outils sensibles.

### Colonne de connexion

| Bouton | Ce qu'il fait |
|---|---|
| **Connecter** | Établit la connexion avec l'instance et la base saisies (ou l'instance seule si le champ base est vide). |
| **Ouvrir fichier local (.mae)** | Sélectionne un fichier `.mae` existant et s'y connecte automatiquement. |

### Contrôle & audit

Outils en lecture seule : ils ne modifient rien.

| Outil | Ce qu'il fait |
|---|---|
| **Triggers spécifiques** | Recherche et affiche les triggers SQL Server présents sur la base qui ne suivent pas la convention de nommage standard Sage — utile pour repérer des personnalisations ou ajouts tiers. |
| **Intégrité comptable** | Lance une série de vérifications sur la comptabilité (comptes ou journaux référencés mais inexistants, écritures déséquilibrées par journal et par mois, écritures analytiques orphelines ou invalides, comptes attendant une ventilation analytique sans en avoir). Résultat affiché dans le journal et, s'il y a des anomalies, dans un fichier texte généré à côté de l'exécutable. |
| **Intégrité gestion co.** | Même principe côté gestion commerciale : tiers, dépôts, collaborateurs ou tiers payeurs référencés mais inexistants, lignes de documents orphelines. Rapport texte si anomalies. |
| **Audit TVA** *(Instance)* | Ouvre un écran listant les bases Sage Comptabilité de l'instance (la base connectée est cochée d'office). Cochez les bases à auditer, choisissez les formats du rapport (Word, PDF, Excel), un rapport unique ou un par base, et le dossier de sortie, puis « Lancer l'analyse ». Les anomalies sont surlignées dans le rapport. |
| **Listing RFE** | Affiche les adresses de facturation électronique (« maillage » : type de maille SIREN, SIRET, SIRET + code routage ou SIREN + suffixe, et adresse correspondante) des clients actifs de la base connectée, regroupées par compte avec un séparateur et le nombre total d'adresses du compte. Les lignes actives sont en vert, les inactives en rouge. Filtres par compte, raison sociale, actif et type de maille ; le bouton « Colonnes » choisit les colonnes affichées ; les numéros de téléphone et e-mails sont cliquables ; une liste déroulante propose les contacts du client. « Exporter Excel... » enregistre la liste filtrée (colonnes affichées) dans un classeur. Nécessite Sage 100 v12 (table des adresses de facturation électronique). |
| **Conformité factures** | Contrôle de conformité d'une facture électronique, sans connexion à une base. Déposez (ou choisissez avec « Parcourir... ») un fichier XML UBL, CII, Factur-X ou CDAR, ou un PDF Factur-X dont le XML est extrait automatiquement. Le profil est détecté automatiquement (EN16931, EXTENDED-CTC-FR, Factur-X, CDAR) ou peut être forcé, puis « Contrôler la conformité » vérifie le document contre les règles officielles France_RFE (FNFE 1.4.0.04) : verdict conforme / non conforme, erreurs bloquantes, avertissements, erreurs de structure XSD et points de vigilance. La liste des règles n'affiche d'abord que celles en échec (cases pour afficher aussi les conformes et les non applicables, recherche par identifiant ou mot-clé) ; un clic sur une règle déplie son contexte, son test et le détail de chaque erreur. |

### Correction de données

| Outil | Ce qu'il fait |
|---|---|
| **Corriger adresses livraison** | Recherche les tiers clients sans adresse de livraison principale et leur en crée une à partir de leur fiche, puis rattache les documents de vente qui pointaient vers une adresse manquante. |
| **Corriger les accents** | Recherche dans les intitulés des comptes et écritures les séquences typiques d'un mauvais encodage (accents mal interprétés) et propose de les corriger, après avoir listé les tables concernées. |
| **Dévalider une facture** *(sensible)* | Affiche la liste des factures de vente validées non comptabilisées : n° de facture, date, n° et intitulé client, montant HT. Cochez une ou plusieurs factures (filtres par dates, client et n° de facture), puis « Dévalider la sélection ». Une facture déjà transmise en facturation électronique apparaît avec un triangle rouge et ne peut pas être dévalidée : elle existe côté Administration Fiscale, il faut l'annuler et refaire une nouvelle facture. |
| **Déclôture d'exercice** *(sensible)* | Liste les exercices comptables du dossier avec un cadenas fermé (clôturé) ou ouvert. Sélectionnez un exercice clôturé, cochez la case de confirmation (le client a confirmé vouloir déclôturer et être conscient des risques) puis validez : l'exercice est rouvert, avec par défaut une sauvegarde automatique de la base avant l'opération (case décochable). Impossible de rouvrir un exercice si un exercice plus récent est encore clôturé. |
| **Créer information libre** | Ajoute un champ personnalisé ("information libre") sur une table Sage : choix de la table, du nom, du type (texte, date, montant...) et, pour le texte, de la longueur (69 caractères par défaut et au maximum, comme dans Sage). Les tables proposées incluent notamment les comptes, les écritures (`F_ECRITUREC`), les articles (`F_ARTICLE`) et les documents. Si la table choisie est `F_DOCENTETE` ou `F_DOCLIGNE`, le même champ est automatiquement créé aussi sur la table d'abonnement correspondante (`F_ABOENTETE`/`F_ABOLIGNE`). |
| **Enrichissement tiers** | Fiabilise les SIRET des fiches tiers en les rapprochant de l'API officielle « Recherche d'entreprises » (INSEE / Sirene) ; nécessite un accès internet. Choisissez Clients, Fournisseurs ou Tous, puis cochez « Valider SIRET existants » pour revérifier les SIRET déjà saisis (sinon l'outil cherche celui des tiers qui n'en ont pas de valide), et lancez la recherche (bouton Stop pour l'interrompre). Chaque ligne reçoit un statut coloré : Valide, Trouvé, Multiples, A vérifier, Radié, Non trouvé, Erreur ou Ignoré. Double-cliquez sur une ligne pour choisir une autre entreprise ou un autre établissement parmi les candidats ; la barre d'espace et le menu contextuel permettent de cocher ou d'ignorer. Seules les lignes cochées sont exportées (« Export Excel ») ou mises à jour : « Mettre à jour Sage 100 » propose de modifier le nom, l'adresse (en majuscules ou non), le n° de TVA, et de mettre en sommeil les tiers radiés, en plus du SIRET. Les valeurs avant / après de chaque fiche sont consignées dans `%APPDATA%\SageToolbox\mises_a_jour_tiers.txt`. |

### Utilisateurs & sécurité

| Outil | Ce qu'il fait |
|---|---|
| **Copier les utilisateurs** *(Instance)* | Copie les utilisateurs et droits d'une base source vers une base destination (utile après une copie de base pour un environnement de test, par exemple) — écrase les utilisateurs existants de la destination, sauf le compte administrateur. |
| **Supprimer mot de passe admin** *(sensible)* | Réinitialise le mot de passe du compte `<Administrateur>` sur tous les modules Sage installés sur la base, avec la possibilité de désactiver en même temps certaines règles de politique de mot de passe (longueur minimale, renouvellement, complexité, etc.) — pratique quand un client a perdu son mot de passe administrateur Sage. |
| **Déconnexion users fantômes** | Repère les sessions Sage restées "actives" en base alors que la connexion SQL correspondante n'existe plus (utilisateur déconnecté brutalement, plantage...) et les nettoie, pour éviter les blocages de licence ou d'accès. |

### Sauvegarde & restauration

Ces outils agissent sur **toute l'instance SQL** et sont disponibles même sans base sélectionnée (connexion "instance seule").

| Outil | Ce qu'il fait |
|---|---|
| **Sauvegarder toutes les bases** *(Instance)* | Demande un dossier de destination et propose d'activer la compression, puis sauvegarde toutes les bases de l'instance (hors bases système SQL Server) en une seule opération. |
| **Restauration SQL en lot** *(Instance, sensible)* | Demande un dossier contenant des fichiers `.bak` et un dossier pour les fichiers de connexion à générer. Restaure toutes les bases du dossier vers l'instance connectée (fichiers de données/journal redirigés automatiquement vers les emplacements par défaut de l'instance et renommés d'après le nom de la base restaurée — fichiers `.mdf`/`.ldf` comme noms logiques ; FILESTREAM éventuel activé en accès complet, répertoire = nom de la base), optimise chaque base restaurée puis génère ses fichiers de connexion — le tout en une seule opération. Une case permet d'autoriser le remplacement des bases déjà existantes. |

### Maintenance technique

Ces deux outils (badge **Instance**) ouvrent d'abord un choix de **portée** : la base connectée, ou toutes les bases Sage de l'instance. Sans base sélectionnée, seule la portée « toutes les bases » est proposée.

| Outil | Ce qu'il fait |
|---|---|
| **Optimiser** | Applique un réglage standard : désactive la fermeture automatique, passe le mode de récupération en "Simple", et aligne le niveau de compatibilité SQL Server sur la version réelle du serveur. Réassocie aussi le compte technique Sage (`USER_CBASE`) à son login si besoin. Si des utilisateurs sont connectés, un avertissement s'affiche dans le journal mais l'opération continue. |
| **Fichiers de connexion** | Demande un dossier de destination, puis y crée les fichiers de connexion Sage (`.mae`, `.gcm`, `.mdp`, `.imo` selon les modules installés, `.prh` si applicable) pour la base connectée ou pour toutes les bases. |

---

*Pour toute question technique sur le fonctionnement interne de l'outil, voir le fichier `README.md` du dépôt.*
