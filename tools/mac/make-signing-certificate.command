#!/bin/bash
# Run once (double-click): makes a free, self-made code signing certificate, "LiveSplit Native
# Signing", in your login keychain. tools/mac/make-app.command then signs every build with it, so
# macOS sees every new build and every update as the same app and keeps its permissions (Input
# Monitoring for the global hotkeys, Accessibility). Without it each build gets a throwaway
# signature and the permission has to be given again.
#
# This is not Apple's notarization: people downloading the app still confirm it once (INSTALL.md).
# The certificate's private key stays in your keychain; only codesign may use it without asking.
# To remove it later: Keychain Access, login keychain, My Certificates, "LiveSplit Native Signing".
set -u
name="LiveSplit Native Signing"
keychain="$HOME/Library/Keychains/login.keychain-db"

if security find-certificate -c "$name" "$keychain" > /dev/null 2>&1; then
  echo "\"$name\" is already in your keychain. Nothing to do."
  read -r -p "Press Return to close." _
  exit 0
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cat > "$work/cert.cnf" <<CNF
[req]
distinguished_name = dn
x509_extensions = codesign
prompt = no
[dn]
CN = $name
[codesign]
basicConstraints = critical, CA:false
keyUsage = critical, digitalSignature
extendedKeyUsage = critical, codeSigning
CNF

echo "Making the certificate (valid for 20 years)..."
/usr/bin/openssl req -x509 -newkey rsa:2048 -nodes -days 7300 \
  -config "$work/cert.cnf" -keyout "$work/key.pem" -out "$work/cert.pem" 2> "$work/openssl.log" || {
  cat "$work/openssl.log"; echo "Could not make the certificate."; read -r -p "Press Return to close." _; exit 1; }
password="$(/usr/bin/openssl rand -hex 16)"
/usr/bin/openssl pkcs12 -export -inkey "$work/key.pem" -in "$work/cert.pem" -name "$name" \
  -out "$work/identity.p12" -passout "pass:$password" 2>> "$work/openssl.log" || {
  cat "$work/openssl.log"; echo "Could not package the certificate."; read -r -p "Press Return to close." _; exit 1; }

echo "Adding it to your login keychain (macOS may ask for your password)..."
security import "$work/identity.p12" -k "$keychain" -P "$password" -T /usr/bin/codesign || {
  echo "Could not add it to the keychain."; read -r -p "Press Return to close." _; exit 1; }

# A test signature, so a problem shows now rather than at the next build.
cp /usr/bin/true "$work/signing-test"
if codesign --force -s "$name" "$work/signing-test" 2> "$work/codesign.log"; then
  echo "Done: \"$name\" signs LiveSplit from the next build on."
  echo "Give the next build the Input Monitoring permission once; after that it stays."
else
  cat "$work/codesign.log"
  echo "The certificate is in your keychain, but codesign could not sign with it (see above)."
fi
read -r -p "Press Return to close." _
