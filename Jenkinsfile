pipeline {
    agent any

    options {
        skipDefaultCheckout()
        disableConcurrentBuilds()
    }

    triggers {
        pollSCM('H/5 * * * *')
    }

    stages {
        stage('Checkout') {
            steps {
                deleteDir()
                checkout scm
                sh 'git rev-parse HEAD'
            }
        }

        stage('Restore') {
            steps {
                sh 'dotnet --version'
                sh 'dotnet restore tests/MensCatalog.Checks/MensCatalog.Checks.csproj'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build tests/MensCatalog.Checks/MensCatalog.Checks.csproj -c Release --no-restore'
            }
        }

        stage('Test') {
            steps {
                script {
                    int result = sh(
                        script: 'dotnet run --project tests/MensCatalog.Checks/MensCatalog.Checks.csproj -c Release --no-build',
                        returnStatus: true
                    )
                    int failures = result == 0 ? 0 : 1
                    String failure = result == 0 ? '' :
                        '<failure message="Xem loi trong Console Output"/>'

                    writeFile file: 'mens-catalog.junit.xml', text: """<?xml version="1.0" encoding="UTF-8"?>
<testsuite name="DiosStore" tests="1" failures="${failures}">
  <testcase classname="DiosStore" name="MensCatalog.Checks">${failure}</testcase>
</testsuite>
"""
                    junit 'mens-catalog.junit.xml'

                    if (result != 0) {
                        error("MensCatalog.Checks failed with exit code ${result}")
                    }
                }
            }
        }
    }
}
