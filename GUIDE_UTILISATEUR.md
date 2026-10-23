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

Deux façons de se connecter, en haut de la fenêtre principale :

- **Instance + base** : renseigner le nom du serveur/instance SQL (ex. `SRV01\SQLEXPRESS`) et le nom de la base Sage, puis cliquer sur **Connecter**. Toutes les fonctions de l'outil deviennent disponibles.
- **Instance seule** : renseigner uniquement l'instance SQL (laisser "Base de données" vide) puis **Connecter**. Seules les fonctions de la carte **Serveur & Instance** (qui agissent sur toute l'instance, pas sur une base précise) sont alors activées — utile par exemple pour sauvegarder ou restaurer un lot de bases sans avoir à se connecter à une base en particulier.
- **Ouvrir fichier local (.mae)** : au lieu de saisir l'instance et la base à la main, sélectionner un fichier `.mae` (fichier de connexion Sage existant) — l'outil en extrait automatiquement le serveur et le nom de base et se connecte.

Une fois connecté, le bandeau de gauche affiche les informations de la base (modules Sage installés, taille des fichiers, emplacement) et de l'instance SQL (version, édition, mode d'authentification, état de FILESTREAM, répertoires par défaut).

### Utiliser une fonction

Chaque bouton correspond à une action. Cliquer dessus ouvre soit directement une **confirmation** (Oui/Non) avant de lancer l'action, soit une **fenêtre de paramétrage** si l'action a besoin d'informations complémentaires (un dossier, un nom, une case à cocher...). Pendant qu'une action tourne, les boutons sont désactivés et le curseur indique une opération en cours.

### Le journal

En bas de la fenêtre, le **journal** affiche en temps réel le déroulé de chaque action (ℹ️ information, ✅ succès, ⚠️ avertissement, ❌ erreur). Il est aussi enregistré dans un fichier sur le poste (`%APPDATA%\SageToolbox\sage_toolbox.log`), consultable après coup. Le **contrôle d'intégrité** génère en plus un fichier `.txt` détaillé à côté de l'exécutable s'il trouve des anomalies.

### Le thème

En bas du bandeau de gauche, un sélecteur permet de choisir l'apparence : **Système** (suit le réglage Windows), **Clair** ou **Sombre**.

---

## Détail des boutons

### Barre de connexion

| Bouton | Ce qu'il fait |
|---|---|
| **Connecter** | Établit la connexion avec l'instance et la base saisies (ou l'instance seule si le champ base est vide). |
| **Ouvrir fichier local (.mae)** | Sélectionne un fichier `.mae` existant et s'y connecte automatiquement. |

### 🔧 Maintenance (Base)

| Bouton | Ce qu'il fait |
|---|---|
| **Optimiser la base** | Applique un réglage standard à la base : désactive la fermeture automatique, passe le mode de récupération en "Simple", et aligne le niveau de compatibilité SQL Server sur la version réelle du serveur. Réassocie aussi le compte technique Sage (`USER_CBASE`) à son login si besoin. Si des utilisateurs sont connectés à la base, un avertissement s'affiche dans le journal mais l'opération continue quand même. |
| **Générer fichiers de connexion** | Demande un dossier de destination, puis y crée les fichiers de connexion Sage (`.mae`, `.gcm`, `.mdp`, `.imo` selon les modules installés, `.prh` si applicable) pour la base connectée. |
| **Lister triggers spécifiques** | Recherche et affiche les triggers SQL Server présents sur la base qui ne suivent pas la convention de nommage standard Sage — utile pour repérer des personnalisations ou ajouts tiers. |
| **Contrôle d'intégrité comptable** | Lance une série de vérifications en lecture seule sur la comptabilité (comptes ou journaux référencés mais inexistants, écritures déséquilibrées par journal et par mois, écritures analytiques orphelines ou invalides, comptes attendant une ventilation analytique sans en avoir). Résultat affiché dans le journal et, s'il y a des anomalies, dans un fichier texte généré à côté de l'exécutable. Ne modifie rien. |
| **Contrôle d'intégrité gestion co.** | Même principe côté gestion commerciale : tiers, dépôts, collaborateurs ou tiers payeurs référencés mais inexistants, lignes de documents orphelines. Lecture seule, rapport texte si anomalies. |

### 💳 Opérations (Base)

| Bouton | Ce qu'il fait |
|---|---|
| **Déclôture d'exercice** | Ouvre une fenêtre listant les exercices comptables du dossier avec leur statut (Ouvert/Clôturé). Sélectionner un exercice clôturé et valider le rouvre — avec, par défaut, une sauvegarde automatique de la base avant l'opération (case décochable). Impossible de rouvrir un exercice si un exercice plus récent est encore clôturé. |
| **Dévalider une facture** | Demande un numéro de pièce et repasse le document correspondant en brouillon (non validé, non imprimé). Si plusieurs documents partagent ce numéro de pièce, une liste permet de choisir le bon avant de continuer. |
| **Créer Information Libre** | Ouvre une fenêtre pour ajouter un champ personnalisé ("information libre") sur une table Sage : choix de la table, du nom, du type (texte, date, montant...) et, pour le texte, de la longueur. Si la table choisie est `F_DOCENTETE` ou `F_DOCLIGNE`, le même champ est automatiquement créé aussi sur la table d'abonnement correspondante (`F_ABOENTETE`/`F_ABOLIGNE`). |
| **Corriger adresses livraison** | Recherche les tiers clients sans adresse de livraison principale et leur en crée une à partir de leur fiche, puis rattache les documents de vente qui pointaient vers une adresse manquante. |
| **Corriger les accents** | Recherche dans les intitulés des comptes et écritures les séquences typiques d'un mauvais encodage (accents mal interprétés) et propose de les corriger, après avoir listé les tables concernées. |

### 🛡️ Administration (Base)

| Bouton | Ce qu'il fait |
|---|---|
| **Supprimer mot de passe admin** | Ouvre une fenêtre qui réinitialise le mot de passe du compte `<Administrateur>` sur tous les modules Sage installés sur la base, avec la possibilité de désactiver en même temps certaines règles de politique de mot de passe (longueur minimale, renouvellement, complexité, etc.) — pratique quand un client a perdu son mot de passe administrateur Sage. |
| **Déconnexion users fantômes** | Repère les sessions Sage restées "actives" en base alors que la connexion SQL correspondante n'existe plus (utilisateur déconnecté brutalement, plantage...) et les nettoie, pour éviter les blocages de licence ou d'accès. |

### ⚙️ Serveur & Instance

Ces fonctions agissent sur **toute l'instance SQL** et sont disponibles même sans base sélectionnée (connexion "instance seule").

| Bouton | Ce qu'il fait |
|---|---|
| **Optimiser TOUTES les bases** | Applique l'action "Optimiser la base" à toutes les bases Sage détectées sur l'instance, l'une après l'autre. |
| **Générer TOUS les fichiers** | Demande un dossier, puis génère les fichiers de connexion pour toutes les bases Sage de l'instance. |
| **Copier les utilisateurs** | Ouvre une fenêtre pour copier les utilisateurs et droits d'une base source vers une base destination (utile après une copie de base pour un environnement de test, par exemple) — écrase les utilisateurs existants de la destination, sauf le compte administrateur. |
| **Restauration SQL en lot** | Ouvre une fenêtre demandant un dossier contenant des fichiers `.bak` et un dossier pour les fichiers de connexion à générer. Restaure toutes les bases du dossier vers l'instance connectée (fichiers de données/journal redirigés automatiquement vers les emplacements par défaut de l'instance et renommés d'après le nom de la base restaurée — fichiers `.mdf`/`.ldf` comme noms logiques ; FILESTREAM éventuel activé en accès complet, répertoire = nom de la base), optimise chaque base restaurée puis génère ses fichiers de connexion — le tout en une seule opération. Une case permet d'autoriser le remplacement des bases déjà existantes. |
| **Sauvegarder TOUTES les bases** | Ouvre une fenêtre demandant un dossier de destination et proposant d'activer la compression, puis sauvegarde toutes les bases de l'instance (hors bases système SQL Server) en une seule opération. |

---

*Pour toute question technique sur le fonctionnement interne de l'outil, voir le fichier `README.md` du dépôt.*
