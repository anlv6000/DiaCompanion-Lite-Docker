
/*chạy từng lệnh*/
/*1*/
ALTER TABLE [DiaCompanion].[dbo].[MedicalVisits]
DROP CONSTRAINT [FK__Visits__PatientI__5FB337D6];
/*2*/
DROP INDEX [IX_Visits_Patient]
ON [DiaCompanion].[dbo].[MedicalVisits];
/*3*/
ALTER TABLE [DiaCompanion].[dbo].[MedicalVisits]
DROP COLUMN [PatientId];